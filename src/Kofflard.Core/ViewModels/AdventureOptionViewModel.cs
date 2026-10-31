using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Models;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// One of the five adventure choices on the search page: an icon with its name small underneath, chosen
/// like a radio button (.my/design/search-page-changes.md).
/// </summary>
public sealed class AdventureOptionViewModel : ParentViewModel
{
    private readonly Action<Adventure> _choose;
    private bool _isChosen;

    /// <summary>Creates a choice.</summary>
    /// <param name="adventure">The level it stands for.</param>
    /// <param name="choose">Called when the user chooses it.</param>
    public AdventureOptionViewModel(Adventure adventure, Action<Adventure> choose)
    {
        Adventure = adventure;
        _choose = choose;
    }

    /// <summary>The level.</summary>
    public Adventure Adventure { get; }

    /// <summary>Its place in the row of five, 0-4.</summary>
    public int Column => Adventure.Level - Adventure.Min;

    /// <summary>"Curious".</summary>
    public string Name => Adventure.Name;

    /// <summary>"adventure_curious.png", or its white "adventure_curious_on.png" while chosen.</summary>
    public string Icon => _isChosen ? Adventure.ChosenIcon : Adventure.Icon;

    /// <summary>Whether this is the chosen level. Checking it chooses it; the others then uncheck.</summary>
    public bool IsChosen
    {
        get => _isChosen;
        set
        {
            if (SetProperty(ref _isChosen, value))
            {
                OnPropertyChanged(nameof(Icon));
                if (value)
                {
                    _choose(Adventure);
                }
            }
        }
    }

    /// <summary>Marks it chosen or not, without choosing it.</summary>
    internal void Show(bool chosen)
    {
        if (SetProperty(ref _isChosen, chosen, nameof(IsChosen)))
        {
            OnPropertyChanged(nameof(Icon));
        }
    }
}
