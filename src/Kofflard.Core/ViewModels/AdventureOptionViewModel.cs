using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Models;

namespace Kofflard.Core.ViewModels;

/// <summary>
/// One stop on the results page's slider, Safe, Curious or Wild, chosen like a radio button
/// (.my/design/adventure-as-outcomes.md).
/// </summary>
public sealed class AdventureOptionViewModel : ParentViewModel
{
    private readonly Action<Lane> _choose;
    private bool _isChosen;

    /// <summary>Creates a stop.</summary>
    /// <param name="lane">The lane it shows up to.</param>
    /// <param name="choose">Called when the user chooses it.</param>
    public AdventureOptionViewModel(Lane lane, Action<Lane> choose)
    {
        Lane = lane;
        _choose = choose;
    }

    /// <summary>The lane.</summary>
    public Lane Lane { get; }

    /// <summary>Its place on the slider, 0-2.</summary>
    public int Column => (int)Lane;

    /// <summary>"Curious".</summary>
    public string Name => Lane.ToString();

    /// <summary>Whether this is the chosen stop. Checking it chooses it; the others then uncheck.</summary>
    public bool IsChosen
    {
        get => _isChosen;
        set
        {
            if (SetProperty(ref _isChosen, value) && value)
            {
                _choose(Lane);
            }
        }
    }

    /// <summary>Marks it chosen or not, without choosing it.</summary>
    internal void Show(bool chosen) => SetProperty(ref _isChosen, chosen, nameof(IsChosen));
}
