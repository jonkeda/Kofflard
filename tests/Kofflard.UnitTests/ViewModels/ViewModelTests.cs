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
    public async Task Search_RemoveRecent_ForgetsEverySearchOfThatName_AndKeepsTheOthers()
    {
        await _history.AddSearchAsync(new RecentSearch("Khruangbin", _time.GetUtcNow()));
        await _history.AddSearchAsync(new RecentSearch("Nick Drake", _time.GetUtcNow()));
        var search = Search();
        await search.LoadAsync();

        await search.RemoveRecentCommand.ExecuteAsync(search.Recent[0]);

        Assert.Equal(["Khruangbin"], search.Recent.Select(r => r.Artist));
        Assert.Equal(["Khruangbin"], _history.Searches.Select(r => r.Artist));
        Assert.True(search.HasRecent);
    }

    [Fact]
    public async Task Search_WithNoAiConnected_GoesToConnect()
    {
        await _connections.DisconnectAsync();

        await Search().LoadAsync();

        Assert.Equal(KofflardRoutes.Connect, Assert.Single(_navigator.Visits).Route);
    }

    [Fact]
    public async Task Results_ShowTheCards_InLanes_WithTheirTotals()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe), ("Ali Farka Touré", Lane.Wild), ("Bridget St John", Lane.Curious), ("Linda Perhacs", Lane.Safe)));
        var results = Results();

        await results.LoadAsync("Nick Drake", cached: false);

        Assert.Equal(ResultsViewModel.ContentState, results.State);
        Assert.Equal(["Vashti Bunyan", "Linda Perhacs", "Bridget St John", "Ali Farka Touré"], results.Cards.Select(i => i.Name));
        Assert.Equal(["Safe (2)", "Curious (1)", "Wild (1)"], results.Lanes.Select(l => l.HeaderText));
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
    public async Task Results_More_AsksAgain_ExcludingWhatWasShown()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);

        Answers(Ok(("Ali Farka Touré", Lane.Wild)));
        await results.MoreCommand.ExecuteAsync(null);

        Assert.Equal("Ali Farka Touré", Assert.Single(results.Cards).Name);
        _engine.Received().DiscoverAsync(
            Azure, "key", "Nick Drake",
            Arg.Is<IReadOnlyCollection<string>>(exclude => exclude.Contains("Vashti Bunyan")),
            Arg.Any<LaneMix?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Results_CardsArrive_OneByOne_AndMoreWaitsForTheWholeAnswer()
    {
        var answer = Ok(("Vashti Bunyan", Lane.Safe), ("Bill Fay", Lane.Curious));
        var gate = new TaskCompletionSource();
        _engine.DiscoverAsync(default!, default, default!, default!, default).ReturnsForAnyArgs(_ => Gated(answer, gate.Task));
        var results = Results();

        var loading = results.LoadAsync("Nick Drake", cached: false);
        await Until(() => results.Cards.Any());

        Assert.Equal(ResultsViewModel.ContentState, results.State);
        Assert.Equal("Vashti Bunyan", Assert.Single(results.Cards).Name);
        Assert.Equal(["Safe (1)"], results.Lanes.Select(l => l.HeaderText));
        Assert.True(results.IsStreaming);
        Assert.False(results.CanAskMore);

        gate.SetResult();
        await loading;

        Assert.Equal(["Vashti Bunyan", "Bill Fay"], results.Cards.Select(c => c.Name));
        Assert.False(results.IsStreaming);
        Assert.True(results.CanAskMore);
    }

    [Fact]
    public async Task Results_WhenTheAnswerBreaksOff_KeepTheCards_AndSayWhy()
    {
        var shown = Ok(("Vashti Bunyan", Lane.Safe)).Suggestions[0];
        _engine.DiscoverAsync(default!, default, default!, default!, default)
            .ReturnsForAnyArgs(_ => Failing(new AiException(AiFailure.Unavailable, "Azure OpenAI / Foundry had a problem on its side. Try again."), shown));
        var results = Results();

        await results.LoadAsync("Nick Drake", cached: false);

        Assert.Equal(ResultsViewModel.ContentState, results.State);
        Assert.Equal("Vashti Bunyan", Assert.Single(results.Cards).Name);
        Assert.True(results.HasStreamError);
        Assert.Contains("problem on its side", results.StreamError, StringComparison.Ordinal);
        Assert.False(results.IsStreaming);
    }

    [Fact]
    public async Task Results_SelectingACard_OpensIt_AndClearsTheSelection()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);
        var card = Assert.Single(results.Cards);

        results.SelectedCard = card;
        await Until(() => card.HasOpenedText);

        Assert.Equal(MusicService.YouTube, Assert.Single(_launcher.Links).Service);
        Assert.Equal("Opened in YouTube", card.OpenedText);
        Assert.Null(results.SelectedCard);
    }

    [Fact]
    public async Task Results_ExcludeWhatWasShownBefore()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        await Results().LoadAsync("Nick Drake", cached: false);

        Answers(Ok(("Linda Perhacs", Lane.Safe)));
        await Results().LoadAsync("Nick Drake", cached: false);

        _engine.Received().DiscoverAsync(
            Azure, "key", "Nick Drake",
            Arg.Is<IReadOnlyCollection<string>>(exclude => exclude.Contains("Vashti Bunyan")),
            Arg.Any<LaneMix?>(),
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
        _engine.DidNotReceiveWithAnyArgs().DiscoverAsync(default!, default, default!, default!, default);
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
        _engine.DiscoverAsync(default!, default, default!, default!, default)
            .ReturnsForAnyArgs(_ => Failing(new AiException(AiFailure.InvalidKey, "This key was not accepted by Azure OpenAI / Foundry.")));
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
        Assert.Equal("Opened in YouTube", card.OpenedText);

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
    public async Task Discover_ASearch_LoadsInPlace_AskingForTheLanesThatAreOn()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe), ("Bridget St John", Lane.Curious)));
        var discover = Discover();
        await discover.LoadAsync();
        Assert.True(discover.ShowsRecent);

        discover.Search.Name = "Nick Drake";
        await discover.Search.SearchCommand.ExecuteAsync(null);

        Assert.Empty(_navigator.Visits);
        Assert.False(discover.ShowsRecent);
        Assert.Equal("Like Nick Drake", discover.Results!.Heading);
        Assert.Equal(["Safe (1)", "Curious (1)"], discover.Results.Lanes.Select(l => l.HeaderText));
        _engine.Received().DiscoverAsync(Azure, "key", "Nick Drake", Arg.Any<IReadOnlyCollection<string>>(), new LaneMix(6, 6, 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discover_ATogglesOff_HidesItsSection_WithNoRequest_AndIsRemembered()
    {
        await _settings.SaveAsync(_settings.Current with { Lanes = LaneSet.All });
        Answers(Ok(("Vashti Bunyan", Lane.Safe), ("Ali Farka Touré", Lane.Wild)));
        var discover = Discover();
        await discover.LoadAsync();
        discover.Search.Name = "Nick Drake";
        await discover.Search.SearchCommand.ExecuteAsync(null);
        _engine.ClearReceivedCalls();

        await discover.Toggles[(int)Lane.Wild].ToggleCommand.ExecuteAsync(null);

        Assert.Equal([Lane.Safe], discover.Results!.Lanes.Select(l => l.Lane));
        Assert.Equal("1 of 2", discover.Results.CountText);
        Assert.Equal("Wild, hidden", discover.Toggles[(int)Lane.Wild].Description);
        Assert.Equal(new LaneSet(true, true, false), _settings.Current.Lanes);
        _engine.DidNotReceiveWithAnyArgs().DiscoverAsync(default!, default, default!, default!, default);

        await discover.Toggles[(int)Lane.Wild].ToggleCommand.ExecuteAsync(null);
        Assert.Equal([Lane.Safe, Lane.Wild], discover.Results.Lanes.Select(l => l.Lane));
        _engine.DidNotReceiveWithAnyArgs().DiscoverAsync(default!, default, default!, default!, default);
    }

    [Fact]
    public async Task Discover_ALaneTurnedOn_WithNoCards_AsksForThatLaneAlone()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe), ("Bridget St John", Lane.Curious)));
        var discover = Discover();
        await discover.LoadAsync();
        discover.Search.Name = "Nick Drake";
        await discover.Search.SearchCommand.ExecuteAsync(null);

        Answers(Ok(("Ali Farka Touré", Lane.Wild)));
        await discover.Toggles[(int)Lane.Wild].ToggleCommand.ExecuteAsync(null);

        Assert.Equal(["Vashti Bunyan", "Bridget St John", "Ali Farka Touré"], discover.Results!.Cards.Select(c => c.Name));
        Assert.Equal([Lane.Safe, Lane.Curious, Lane.Wild], discover.Results.Lanes.Select(l => l.Lane));
        _engine.Received().DiscoverAsync(Azure, "key", "Nick Drake", Arg.Any<IReadOnlyCollection<string>>(), LaneMix.Only(Lane.Wild), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discover_ALaneTurnedOn_WhileTheSearchRuns_IsAskedForWhenItEnds()
    {
        var gate = new TaskCompletionSource();
        var first = Ok(("Vashti Bunyan", Lane.Safe), ("Bridget St John", Lane.Curious));
        _engine.DiscoverAsync(default!, default, default!, default!, default).ReturnsForAnyArgs(_ => Gated(first, gate.Task));
        var discover = Discover();
        await discover.LoadAsync();
        discover.Search.Name = "Nick Drake";
        var searching = discover.Search.SearchCommand.ExecuteAsync(null);
        await Until(() => discover.Results is { HasContent: true });

        await discover.Toggles[(int)Lane.Wild].ToggleCommand.ExecuteAsync(null);
        Answers(Ok(("Ali Farka Touré", Lane.Wild)));
        gate.SetResult();
        await searching;

        await Until(() => discover.Results!.Has(Lane.Wild));
        Assert.Equal("Ali Farka Touré", Assert.Single(discover.Results!.LaneColumns[(int)Lane.Wild].Cards).Name);
    }

    [Fact]
    public async Task Discover_TheLastLaneOn_CannotGoOff()
    {
        await _settings.SaveAsync(_settings.Current with { Lanes = new LaneSet(false, false, true) });
        var discover = Discover();
        await discover.LoadAsync();

        await discover.Toggles[(int)Lane.Wild].ToggleCommand.ExecuteAsync(null);

        Assert.True(discover.Toggles[(int)Lane.Wild].IsOn);
        Assert.Equal(new LaneSet(false, false, true), discover.Lanes);
    }

    [Fact]
    public async Task Discover_ACardsSearch_ReplacesTheList_AndBackGoesThroughTheTrail_FromTheCache()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var discover = Discover();
        await discover.LoadAsync();
        discover.Search.Name = "Nick Drake";
        await discover.Search.SearchCommand.ExecuteAsync(null);

        Answers(Ok(("Linda Perhacs", Lane.Safe)));
        await discover.Results!.Cards.First().ExploreCommand.ExecuteAsync(null);

        Assert.Empty(_navigator.Visits);
        Assert.Equal("Like Vashti Bunyan", discover.Results!.Heading);
        Assert.Equal("Nick Drake", discover.TrailText);
        Assert.Equal("Vashti Bunyan", discover.Search.Name);

        _engine.ClearReceivedCalls();
        Assert.True(await discover.BackAsync());

        Assert.Equal("Like Nick Drake", discover.Results!.Heading);
        Assert.Equal("Vashti Bunyan", Assert.Single(discover.Results.Cards).Name);
        Assert.False(discover.HasTrail);
        _engine.DidNotReceiveWithAnyArgs().DiscoverAsync(default!, default, default!, default!, default);

        Assert.True(await discover.BackAsync());
        Assert.True(discover.ShowsRecent);
        Assert.False(await discover.BackAsync());
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

    [Fact]
    public async Task Results_TheDesktopsColumns_HoldEveryLane_AndTheMapTheShownCards()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe), ("Bridget St John", Lane.Curious), ("Ali Farka Touré", Lane.Wild)));
        var results = Results();
        await results.LoadAsync("Nick Drake", cached: false);

        Assert.Equal(["Safe (1)", "Curious (1)", "Wild (1)"], results.LaneColumns.Select(column => column.HeaderText));
        Assert.Equal("ND", results.ArtistInitials);
        Assert.Equal("VB", results.Cards.First().Initials);
        Assert.Equal(["Vashti Bunyan", "Bridget St John", "Ali Farka Touré"], results.MapNodes.Select(node => node.Name));

        results.Shown = new LaneSet(true, true, false);
        Assert.Equal("Ali Farka Touré", Assert.Single(results.LaneColumns[(int)Lane.Wild].Cards).Name);
        Assert.Equal(["Vashti Bunyan", "Bridget St John"], results.MapNodes.Select(node => node.Name));

    }

    [Fact]
    public async Task Discover_AMapNode_SearchesThatMusician_OnTheTrail()
    {
        Answers(Ok(("Vashti Bunyan", Lane.Safe)));
        var discover = Discover();
        await discover.LoadAsync();
        discover.Search.Name = "Nick Drake";
        await discover.Search.SearchCommand.ExecuteAsync(null);

        Answers(Ok(("Linda Perhacs", Lane.Safe)));
        discover.Results!.MapNodes[0].ExploreCommand.Execute(null);
        await Until(() => discover.Results.Cards.Any(card => card.Name == "Linda Perhacs"));

        Assert.Equal("Like Vashti Bunyan", discover.Results.Heading);
        Assert.Equal("Nick Drake", discover.TrailText);
    }

        private SearchViewModel Search() => new(_discovery, _connections, _navigator);

    private ResultsViewModel Results()
        => new(_discovery, _settings, _saved, _verifier, new SuggestionCards(_launcher, _history, _saved, _time), _connections, _navigator);

    private DiscoverViewModel Discover() => new(Search(), Results, _settings);

    private ConnectViewModel Connect()
        => new(_connections, _engine, Substitute.For<IOpenRouterSignIn>(), _launcher, _navigator);

    // As the engine answers: each lane's share as a pick first, then the whole result.
    private void Answers(DiscoveryResult result)
        => _engine.DiscoverAsync(default!, default, default!, default!, default).ReturnsForAnyArgs(_ => Streamed(result));

    private static async IAsyncEnumerable<DiscoveryUpdate> Streamed(DiscoveryResult result)
    {
        await Task.Yield();
        if (result.Artist is { } artist)
        {
            yield return new DiscoveryUpdate.ArtistFound(artist);
        }

        if (result.Status == DiscoveryStatus.Ok)
        {
            foreach (var lane in result.Suggestions.GroupBy(s => s.Lane))
            {
                foreach (var pick in lane.Take(LaneMix.For(lane.Key)))
                {
                    yield return new DiscoveryUpdate.PickFound(pick);
                }
            }
        }

        yield return new DiscoveryUpdate.Done(result);
    }

    private static async IAsyncEnumerable<DiscoveryUpdate> Failing(AiException failure, params Suggestion[] first)
    {
        await Task.Yield();
        foreach (var pick in first)
        {
            yield return new DiscoveryUpdate.PickFound(pick);
        }

        throw failure;
    }

    private static async IAsyncEnumerable<DiscoveryUpdate> Gated(DiscoveryResult result, Task gate)
    {
        yield return new DiscoveryUpdate.PickFound(result.Suggestions[0]);
        await gate;
        foreach (var pick in result.Suggestions.Skip(1))
        {
            yield return new DiscoveryUpdate.PickFound(pick);
        }

        yield return new DiscoveryUpdate.Done(result);
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var waited = 0; !condition() && waited < 5000; waited += 10)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static DiscoveryResult Ok(params (string Name, Lane Lane)[] picks)
        => new(
            DiscoveryStatus.Ok,
            new Musician("Nick Drake", "English folk singer-songwriter"),
            [.. picks.Select(p => new Suggestion(p.Name, p.Lane, $"Why {p.Name}", p.Lane == Lane.Wild ? "A concrete bridge to the original artist" : null, ["folk"], null)).OrderBy(s => s.Lane)],
            []);
}
