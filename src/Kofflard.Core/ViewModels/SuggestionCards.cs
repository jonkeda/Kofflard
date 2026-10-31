using Kofflard.Core.Models;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>Makes cards, with the services every card needs.</summary>
public sealed class SuggestionCards(IMusicLauncher launcher, IHistoryStore history, ISavedStore saved, TimeProvider time)
{
    /// <summary>A card for a suggestion.</summary>
    public SuggestionViewModel Create(
        Suggestion suggestion,
        MusicService openWith,
        bool isSaved,
        Func<SuggestionViewModel, Task> explore)
        => new(suggestion, openWith, isSaved, launcher, history, saved, time, explore);

    /// <summary>A card for a saved musician.</summary>
    public SuggestionViewModel Create(SavedMusician musician, MusicService openWith, Func<SuggestionViewModel, Task> explore)
    {
        var card = Create(
            new Suggestion(musician.Name, Lane.Close, musician.Why, null, musician.Tags, musician.KnownFor),
            openWith,
            isSaved: true,
            explore);

        if (musician.SpotifyArtistId is { } spotify)
        {
            card.Verification = new Verification(VerificationState.Verified, null, spotify);
        }

        return card;
    }
}
