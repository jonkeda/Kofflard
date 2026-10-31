using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;
using Kofflard.Core.ViewModels;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Kofflard.UnitTests.ViewModels;

/// <summary>The view models over in-memory stores and a substitute AI.</summary>
public sealed class ViewModelTests
{
    private static readonly AiConnection Azure = new("azure", "https://res.openai.azure.com", "gpt-4.1-mini");

    private readonly MemorySettings _settings = new(AppSettings.Default with { Connection = Azure });
    private readonly InMemorySecretStore _secrets = new();
    private readonly MemoryHistory _history = new();
    private readonly MemorySaved _saved = new();
    private readonly RecordingLauncher _launcher = new();
    private readonly RecordingNavigator _navigator = new();
    private readonly ScriptedDialogs _dialogs = new();
    private readonly IDiscoveryEngine _engine = Substitute.For<IDiscoveryEngine>();
    private readonly IArtistVerifier _verifier = Substitute.For<IArtistVerifier>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero));
    private readonly AiConnections _connections;
    private readonly DiscoveryService _discovery;

    public ViewModelTests()
    {
        _secrets.SetAsync(AiConnections.SecretName, "key").GetAwaiter().GetResult();
        _connections = new AiConnections(_settings, _secrets);
        _discovery = new DiscoveryService(_connections, _engine, _history, _time);
    }

    [Fact]
    public async Task Search_WithNoName_ShowsAnError_AndStays()
    {
        var search = Search();

        await search.SearchCommand.ExecuteAsync(null);

        Assert.Equal(SearchViewModel.NameRequired, search.NameError);
        Assert.Empty(_navigator.Visits);
    }

    [Fact]
    public async Task Search_OpensTheResults_ForTheCleanedName()
    {
        var search = Search();
        await search.LoadAsync();
        search.Name = "  Nick   Drake ";

        await search.SearchCommand.ExecuteAsync(null);

        var (route, parameters) = Assert.Single(_navigator.Visits);
        Assert.Equal(KofflardRoutes.Results, route);
        Assert.Equal("Nick Drake", parameters![KofflardRoutes.ArtistParameter]);
        Assert.Equal([KofflardRoutes.ArtistParameter, KofflardRoutes.CachedParameter], parameters.Keys.Order());
    }

    [Fact]
    public async Task Search_WithNoAiConnected_GoesToConnect()
    {
        await _connections.DisconnectAsync();

        await Search().LoadAsync();

        Assert.Equal(KofflardRoutes.Connect, Assert.Single(_navigator.Visits).Route);
    }

    [Fact]
    public async Task Results_ShowTheCards_InLanes_OnlySafeAtFirst_WithTheirTotals()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe), ("Ali Farka Touré", Lane.Wild), ("Bridget St John", Lane.Curious), ("Linda Perhacs", Lane.Safe)));
        var results = Results();

        await results.LoadAsync("Nick Drake", cached: false);

        Assert.Equal(ResultsViewModel.ContentState, results.State);
        Assert.Equal(["Vashti Bunyan", "Linda Perhacs", "Bridget St John", "Ali Farka Touré"], results.Cards.Select(i => i.Name));
        Assert.Equal(["Safe (2)", "Curious (1)", "Wild (1)"], results.Lanes.Select(l => l.HeaderText));
        Assert.Equal([2, 0, 0], results.Lanes.Select(l => l.Count));
        Assert.Equal(["Safe", "Curious", "Wild"], results.AdventureOptions.Select(o => o.Name));
        Assert.Equal("Safe", Assert.Single(results.AdventureOptions, o => o.IsChosen).Name);
        Assert.Equal("Like Nick Drake", results.Heading);
    }

    [Fact]
    public async Task Results_ALaneThatHasNoSuggestions_IsNotShown()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();

        await results.LoadAsync("Nick Drake", cached: false);

        Assert.Equal([Lane.Safe], results.Lanes.Select(l => l.Lane));
    }

    [Fact]
    public async Task Results_SlidingToCurious_ShowsItsCards_WithNoNewRequest_AndTheNextResultsStartThere()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe), ("Bridget St John", Lane.Curious), ("Ali Farka Touré", Lane.Wild)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);
        _engine.ClearReceivedCalls();

        results.AdventureOptions[1].IsChosen = true;
        await results.Remembering;

        Assert.Equal(Lane.Curious, results.Adventure);
        Assert.Equal("Curious", results.AdventureName);
        Assert.Equal(["Vashti Bunyan", "Bridget St John"], results.Lanes.SelectMany(l => l).Select(c => c.Name));
        Assert.Equal(Lane.Curious, _settings.Current.Adventure);
        await _engine.DidNotReceiveWithAnyArgs().DiscoverAsync(default!, default, default!, default, default!, default);

        var next = Results();
        await next.LoadAsync("Nick Drake", cached: true);
        Assert.Equal([true, true, false], next.Lanes.Select(l => l.IsShown));
        Assert.Equal("Curious", Assert.Single(next.AdventureOptions, o => o.IsChosen).Name);
    }

    [Fact]
    public async Task Results_SlidingBackToSafe_HidesTheOthers_AndKeepsTheirTotals()
    {
        await _settings.SaveAsync(_settings.Current with { Adventure = Lane.Wild });
        Answers(Ok(("Vashti Bunyan", Lane.Safe), ("Bridget St John", Lane.Curious), ("Ali Farka Touré", Lane.Wild)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);
        Assert.Equal(3, results.Lanes.Sum(l => l.Count));

        results.AdventureOptions[0].IsChosen = true;
        await results.Remembering;

        Assert.Equal([1, 0, 0], results.Lanes.Select(l => l.Count));
        Assert.Equal(["Safe (1)", "Curious (1)", "Wild (1)"], results.Lanes.Select(l => l.HeaderText));
        Assert.Equal(Lane.Safe, _settings.Current.Adventure);
    }

    [Fact]
    public async Task Results_ASearch_AsksForTheEvenMix_AndMoreLeansToTheSlider()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);

        await _engine.Received().DiscoverAsync(Azure, "key", "Nick Drake", LaneMix.Even, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());

        results.AdventureOptions[2].IsChosen = true;
        Answers(Ok(("Ali Farka Touré", Lane.Wild)));
        await results.MoreCommand.ExecuteAsync(null);

        await _engine.Received().DiscoverAsync(Azure, "key", "Nick Drake", Lane.Wild, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Results_ExcludeWhatWasShownBefore()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        await Results().LoadAsync("Nick Drake", cached: false);

        Answers(Ok(("Linda Perhacs", Lane.Safe)));
        await Results().LoadAsync("Nick Drake", cached: false);

        await _engine.Received().DiscoverAsync(
            Azure, "key", "Nick Drake", LaneMix.Even,
            Arg.Is<IReadOnlyCollection<string>>(exclude => exclude.Contains("Vashti Bunyan")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Results_ForARecentSearch_ComeFromTheCache_WithinADay()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        await Results().LoadAsync("Nick Drake", cached: false);
        _engine.ClearReceivedCalls();

        _time.Advance(TimeSpan.FromHours(23));
        var again = Results();
        await again.LoadAsync("Nick Drake", cached: true);

        Assert.Equal("Vashti Bunyan", Assert.Single(again.Cards).Name);
        await _engine.DidNotReceiveWithAnyArgs().DiscoverAsync(default!, default, default!, default, default!, default);
    }

    [Fact]
    public async Task Results_ForAnAmbiguousName_AskWhichOne_AndSearchThePick()
    {
        Answers(new DiscoveryResult(DiscoveryStatus.Ambiguous, null, [], [new("Genesis", "English prog rock band"), new("Genesis Owusu", "Australian rapper")]));
        var results = Results();
        await results.LoadAsync("Genesis", cached: false);

        Assert.Equal(ResultsViewModel.CandidatesState, results.State);
        Assert.Equal("Which Genesis do you mean?", results.CandidatesTitle);

        Answers(Ok(("Little Simz", Lane.Safe)));
        await results.ChooseCandidateCommand.ExecuteAsync(results.Candidates[1]);

        Assert.Equal("Like Genesis Owusu", results.Heading);
        Assert.Equal("Little Simz", Assert.Single(results.Cards).Name);
    }

    [Fact]
    public async Task Results_WhenTheKeyIsRefused_ShowTheError_AndOfferToFixIt()
    {
        _engine.DiscoverAsync(default!, default, default!, default, default!, default)
            .ReturnsForAnyArgs<Task<DiscoveryResult>>(_ => throw new AiException(AiFailure.InvalidKey, "This key was not accepted by Azure OpenAI / Foundry."));
        var results = Results();

        await results.LoadAsync("Nick Drake", cached: false);

        Assert.Equal(ResultsViewModel.ErrorState, results.State);
        Assert.Equal("This key was not accepted by Azure OpenAI / Foundry.", results.ErrorMessage);
        Assert.True(results.CanFixConnection);
        Assert.Equal($"AI: {Azure.Describe()}", results.ConnectionLine);
    }

    [Fact]
    public async Task Results_CheckNames_OnlyWhenTheSettingIsOn()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        _verifier.VerifyAsync("Vashti Bunyan", Arg.Any<CancellationToken>()).Returns(new Verification(VerificationState.Verified, "mbid", "4aF"));

        var off = Results();
        await off.LoadAsync("Nick Drake", cached: false);
        await off.Verifying;
        Assert.False(off.Cards.First().IsVerified);

        await _settings.SaveAsync(_settings.Current with { CheckNames = true });
        var on = Results();
        await on.LoadAsync("Nick Drake", cached: true);
        await on.Verifying;
        Assert.True(on.Cards.First().IsVerified);
        Assert.Equal("https://open.spotify.com/artist/4aF", on.Cards.First().Link(MusicService.Spotify).WebUrl.AbsoluteUri);
    }

    [Fact]
    public async Task Card_Tap_OpensTheOpenWithService_AndALongPress_OffersTheOthers()
    {
        await _settings.SaveAsync(_settings.Current with { OpenWith = MusicService.Spotify });
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);
        var card = results.Cards.First();

        Assert.Equal("Open Vashti Bunyan in Spotify", card.OpenDescription);
        Assert.False(card.ShowsOtherServices);
        await card.OpenCommand.ExecuteAsync(null);

        card.ShowOtherServicesCommand.Execute(null);
        Assert.True(card.ShowsOtherServices);
        Assert.Equal("YouTube", card.SecondText);
        Assert.Equal("YouTube Music", card.ThirdText);
        await card.OpenThirdCommand.ExecuteAsync(null);

        Assert.False(card.ShowsOtherServices);
        Assert.Equal([MusicService.Spotify, MusicService.YouTubeMusic], _launcher.Links.Select(l => l.Service));
        Assert.Equal(["Vashti Bunyan", "Vashti Bunyan"], _history.Opened);
    }

    [Fact]
    public async Task Card_ASecondLongPress_HidesTheOtherServices()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);
        var card = results.Cards.First();

        card.ShowOtherServicesCommand.Execute(null);
        card.ShowOtherServicesCommand.Execute(null);

        Assert.False(card.ShowsOtherServices);
        Assert.Empty(_launcher.Links);
    }

    [Fact]
    public async Task Card_SaysWhereItOpened_ForAFewSeconds()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);
        var card = results.Cards.First();

        await card.OpenCommand.ExecuteAsync(null);
        Assert.Equal("Opened in YouTube ✓", card.OpenedText);

        _time.Advance(SuggestionViewModel.OpenedShownFor);
        await Task.Yield();
        Assert.False(card.HasOpenedText);
    }

    [Fact]
    public async Task Card_Star_TogglesFavourite()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);
        var card = results.Cards.First();

        Assert.Equal("☆", card.StarText);
        Assert.Equal("Add Vashti Bunyan to favourites", card.StarDescription);

        await card.SaveCommand.ExecuteAsync(null);
        Assert.True(card.IsSaved);
        Assert.Equal("★", card.StarText);
        Assert.Equal("Remove Vashti Bunyan from favourites", card.StarDescription);
        Assert.Equal("Vashti Bunyan", Assert.Single(_saved.Items).Name);

        await card.SaveCommand.ExecuteAsync(null);
        Assert.False(card.IsSaved);
        Assert.Empty(_saved.Items);
    }

    [Fact]
    public async Task Card_Explore_SearchesFromThatMusician()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);

        await results.Cards.First().ExploreCommand.ExecuteAsync(null);

        var (route, parameters) = Assert.Single(_navigator.Visits);
        Assert.Equal(KofflardRoutes.Results, route);
        Assert.Equal("Vashti Bunyan", parameters![KofflardRoutes.ArtistParameter]);
    }

    [Fact]
    public async Task Connect_StartsOnAzure_AndAsksForItsEndpoint()
    {
        var connect = Connect();

        Assert.Equal("Azure OpenAI / Foundry", connect.Provider.Name);
        Assert.True(connect.NeedsEndpoint);
        Assert.Equal("Deployment", connect.ModelLabel);

        await connect.TestCommand.ExecuteAsync(null);
        Assert.True(connect.StatusIsError);
        Assert.StartsWith("Enter the endpoint", connect.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Connect_WithAGoodKey_KeepsTheConnection_AndGoesToSearch()
    {
        await _connections.DisconnectAsync();
        var connect = Connect();
        connect.ProviderIndex = AiProviders.All.ToList().FindIndex(p => p.Id == "openai");
        connect.ApiKey = "sk-good";

        await connect.DoneCommand.ExecuteAsync(null);

        Assert.Equal("openai", _settings.Current.Connection!.ProviderId);
        Assert.Equal("gpt-4.1-mini", _settings.Current.Connection.Model);
        Assert.Equal("sk-good", await _secrets.GetAsync(AiConnections.SecretName));
        Assert.Equal("//search", Assert.Single(_navigator.Visits).Route);
        Assert.Empty(connect.ApiKey);
    }

    [Fact]
    public async Task Connect_WithARefusedKey_SaysSo_AndKeepsNothing()
    {
        await _connections.DisconnectAsync();
        _engine.TestAsync(default!, default, default).ThrowsAsyncForAnyArgs(new AiException(AiFailure.InvalidKey, "This key was not accepted by OpenAI."));
        var connect = Connect();
        connect.ProviderIndex = AiProviders.All.ToList().FindIndex(p => p.Id == "openai");
        connect.ApiKey = "sk-wrong";

        await connect.DoneCommand.ExecuteAsync(null);

        Assert.Equal("This key was not accepted by OpenAI.", connect.Status);
        Assert.Null(_settings.Current.Connection);
        Assert.Empty(_navigator.Visits);
    }

    [Fact]
    public async Task Settings_DefaultToYouTube_NoNameChecks_AndKeepChangesAtOnce()
    {
        var settings = new SettingsViewModel(_settings, _connections, _history, _dialogs, _navigator);
        await settings.LoadAsync();

        Assert.Equal(0, settings.OpenWithIndex);
        Assert.Equal("YouTube", settings.ServiceNames[settings.OpenWithIndex]);
        Assert.False(settings.CheckNames);

        settings.OpenWithIndex = 1;
        settings.CheckNames = true;

        Assert.Equal(MusicService.YouTubeMusic, _settings.Current.OpenWith);
        Assert.True(_settings.Current.CheckNames);
    }

    [Fact]
    public async Task Settings_Disconnect_ForgetsTheKey_AfterAsking()
    {
        var settings = new SettingsViewModel(_settings, _connections, _history, _dialogs, _navigator);
        await settings.LoadAsync();

        await settings.DisconnectCommand.ExecuteAsync(null);

        Assert.Contains(SettingsViewModel.DisconnectTitle, _dialogs.Shown);
        Assert.Null(await _secrets.GetAsync(AiConnections.SecretName));
        Assert.Equal("No AI connected", settings.ConnectionLine);
    }

    private SearchViewModel Search() => new(_discovery, _connections, _navigator);

    private ResultsViewModel Results()
        => new(_discovery, _settings, _saved, _verifier, new SuggestionCards(_launcher, _history, _saved, _time), _connections, _navigator);

    private ConnectViewModel Connect()
        => new(_connections, _engine, Substitute.For<IOpenRouterSignIn>(), _launcher, _navigator);

    private void Answers(DiscoveryResult result)
        => _engine.DiscoverAsync(default!, default, default!, default, default!, default).ReturnsForAnyArgs(result);

    private static DiscoveryResult Ok(params (string Name, Lane Lane)[] picks)
        => new(
            DiscoveryStatus.Ok,
            new Musician("Nick Drake", "English folk singer-songwriter"),
            [.. picks.Select(p => new Suggestion(p.Name, p.Lane, $"Why {p.Name}", p.Lane == Lane.Wild ? "A concrete bridge to the original artist" : null, ["folk"], null)).OrderBy(s => s.Lane)],
            []);
}
