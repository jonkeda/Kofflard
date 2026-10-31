using System.Collections.ObjectModel;
using System.ComponentModel;
using Kofflard.Core.Models;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// One lane on the results page, a group headed "Safe (4)". The slider decides whether it shows
/// (.my/design/adventure-as-outcomes.md).
/// </summary>
/// <remarks>
/// A hidden lane is empty rather than hidden, so the list keeps virtualising and no hidden card is left for
/// automation to find; it keeps its cards and shows them again when the slider comes back.
/// </remarks>
public sealed class LaneGroupViewModel : ObservableCollection<SuggestionViewModel>
{
    private readonly IReadOnlyList<SuggestionViewModel> _cards;
    private bool _isShown;

    /// <summary>Creates a lane.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cards">Its cards, in order.</param>
    /// <param name="isShown">Whether it starts shown.</param>
    public LaneGroupViewModel(Lane lane, IReadOnlyList<SuggestionViewModel> cards, bool isShown)
    {
        Lane = lane;
        _cards = cards;
        IsShown = isShown;
    }

    /// <summary>The lane.</summary>
    public Lane Lane { get; }

    /// <summary>"Safe", "Curious" or "Wild".</summary>
    public string Name => Lane.ToString();

    /// <summary>The lane's suggestions, shown or not.</summary>
    public int Total => _cards.Count;

    /// <summary>Every card in the lane, shown or not.</summary>
    public IReadOnlyList<SuggestionViewModel> Cards => _cards;

    /// <summary>"Safe (4)".</summary>
    public string HeaderText => $"{Name} ({Total})";

    /// <summary>Whether the cards are shown.</summary>
    public bool IsShown
    {
        get => _isShown;
        set
        {
            if (_isShown == value && Items.Count == (value ? _cards.Count : 0))
            {
                return;
            }

            _isShown = value;
            Clear();
            if (value)
            {
                foreach (var card in _cards)
                {
                    Add(card);
                }
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsShown)));
        }
    }
}
