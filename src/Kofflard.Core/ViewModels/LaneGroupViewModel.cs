using System.Collections.ObjectModel;
using System.ComponentModel;
using Bruinet.Samples.Shared.Commands;
using Kofflard.Core.Models;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// One lane on the results page, a group that folds: "▸ Close (6)" (.my/design/results-list-changes.md).
/// </summary>
/// <remarks>
/// A closed lane is empty rather than hidden, so the list keeps virtualising and no hidden card is left for
/// automation to find; it keeps its cards and shows them again when opened.
/// </remarks>
public sealed class LaneGroupViewModel : ObservableCollection<SuggestionViewModel>
{
    private readonly IReadOnlyList<SuggestionViewModel> _cards;
    private readonly Action<LaneGroupViewModel> _toggled;
    private bool _isExpanded;

    /// <summary>Creates a lane.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cards">Its cards, in order.</param>
    /// <param name="isExpanded">Whether it starts open.</param>
    /// <param name="toggled">Told after the user opens or closes it.</param>
    public LaneGroupViewModel(Lane lane, IReadOnlyList<SuggestionViewModel> cards, bool isExpanded, Action<LaneGroupViewModel> toggled)
    {
        Lane = lane;
        _cards = cards;
        _toggled = toggled;
        ToggleCommand = new RelayCommand(Toggle);
        IsExpanded = isExpanded;
    }

    /// <summary>The lane.</summary>
    public Lane Lane { get; }

    /// <summary>"Close", "Stretch" or "Leap".</summary>
    public string Name => Lane.ToString();

    /// <summary>The lane's suggestions, open or closed.</summary>
    public int Total => _cards.Count;

    /// <summary>Every card in the lane, shown or not.</summary>
    public IReadOnlyList<SuggestionViewModel> Cards => _cards;

    /// <summary>"▸ Close (6)" closed, "▾ Close (6)" open.</summary>
    public string HeaderText => $"{(_isExpanded ? "▾" : "▸")} {Name} ({Total})";

    /// <summary>Whether the cards are shown.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value && Items.Count == (value ? _cards.Count : 0))
            {
                return;
            }

            _isExpanded = value;
            Clear();
            if (value)
            {
                foreach (var card in _cards)
                {
                    Add(card);
                }
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsExpanded)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HeaderText)));
        }
    }

    /// <summary>Opens or closes the lane.</summary>
    public IRelayCommand ToggleCommand { get; }

    private void Toggle()
    {
        IsExpanded = !IsExpanded;
        _toggled(this);
    }
}
