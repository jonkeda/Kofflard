using System.Collections.ObjectModel;
using Kofflard.Core.Models;

namespace Kofflard.Core.ViewModels;

/// <summary>One lane on the results page, a group headed "Safe (4)" (.my/design/adventure-as-outcomes.md).</summary>
public sealed class LaneGroupViewModel : ObservableCollection<SuggestionViewModel>
{
    /// <summary>Creates a lane.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cards">Its cards, in order.</param>
    public LaneGroupViewModel(Lane lane, IReadOnlyList<SuggestionViewModel> cards)
        : base(cards)
    {
        Lane = lane;
    }

    /// <summary>The lane.</summary>
    public Lane Lane { get; }

    /// <summary>"Safe", "Curious" or "Wild".</summary>
    public string Name => Lane.ToString();

    /// <summary>Every card in the lane.</summary>
    public IReadOnlyList<SuggestionViewModel> Cards => Items.ToList();

    /// <summary>"Safe (4)".</summary>
    public string HeaderText => $"{Name} ({Count})";
}
