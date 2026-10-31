using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// One lane on the results page, a group headed "Safe (4)" (.my/design/adventure-as-outcomes.md). Cards are
/// added as the AI writes them (.my/design/streaming-answers.md), and the header counts up.
/// </summary>
public sealed class LaneGroupViewModel : ObservableCollection<SuggestionViewModel>
{
    /// <summary>Creates a lane.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cards">Its cards so far, in order.</param>
    public LaneGroupViewModel(Lane lane, IEnumerable<SuggestionViewModel> cards)
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

    /// <inheritdoc />
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnCollectionChanged(e);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HeaderText)));
    }
}

/// <summary>
/// One of the desktop's three lane columns (.my/design/one-page-desktop.md): every lane has one, with its
/// cards, whether its toggle is on or off.
/// </summary>
public sealed class LaneColumnViewModel : INotifyPropertyChanged
{
    /// <summary>Creates a column with no cards.</summary>
    public LaneColumnViewModel(Lane lane)
    {
        Lane = lane;
        Cards.CollectionChanged += (_, _) =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        };
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The lane.</summary>
    public Lane Lane { get; }

    /// <summary>"Safe", "Curious" or "Wild".</summary>
    public string Name => Lane.ToString();

    /// <summary>The lane's cards, in order.</summary>
    public ObservableCollection<SuggestionViewModel> Cards { get; } = [];

    /// <summary>How many cards.</summary>
    public int Count => Cards.Count;

    /// <summary>"Safe (4)", as the phone's group header reads.</summary>
    public string HeaderText => $"{Name} ({Cards.Count})";

    /// <summary>What a column says when its lane is off.</summary>
    public string OffText => $"{Name} is off. Click {Name} to ask for {Name.ToLowerInvariant()} picks.";
}

/// <summary>A pick on the desktop's artist map: a dot in its lane's colour, which searches it when clicked.</summary>
/// <param name="node">Where it is.</param>
/// <param name="explore">Its card's "More →".</param>
public sealed class MapNodeViewModel(ArtistMap.Node node, ICommand explore)
{
    /// <summary>The musician.</summary>
    public string Name => node.Name;

    /// <summary>"Safe", "Curious" or "Wild", for the colour.</summary>
    public string LaneName => node.Lane.ToString();

    /// <summary>The dot's box in the map: 18 wide, centred on the node.</summary>
    public Rect DotBounds => new(node.X - 9, node.Y - 9, 18, 18);

    /// <summary>The name's box, under the dot.</summary>
    public Rect LabelBounds => new(node.X - 60, node.Y + 10, 120, 16);

    /// <summary>The line from the middle of the map to the dot.</summary>
    public (double X, double Y) From => ArtistMap.Centre;

    /// <summary>The dot's middle, from the left.</summary>
    public double X => node.X;

    /// <summary>The dot's middle, from the top.</summary>
    public double Y => node.Y;

    /// <summary>Searches this musician, as its card's "More →".</summary>
    public ICommand ExploreCommand => explore;
}

/// <summary>A box on the map, in the map's units.</summary>
public readonly record struct Rect(double X, double Y, double Width, double Height);
