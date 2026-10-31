using System.Collections.ObjectModel;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Models;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>The favourites, with the same card as the results.</summary>
public sealed class SavedViewModel(ISavedStore saved, ISettingsStore settings, SuggestionCards cards, INavigator navigator, ISearchFlow flow) : ParentViewModel
{
    private SuggestionViewModel? _selectedCard;

    /// <summary>The saved musicians, newest first.</summary>
    public ObservableCollection<SuggestionViewModel> Items { get; } = [];

    /// <summary>
    /// Clicking or tapping a card selects it, which opens it in the Open with service; the selection is then
    /// cleared, as Bruinet's Todo sample does (.my/rca/card-click.md).
    /// </summary>
    public SuggestionViewModel? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (!SetProperty(ref _selectedCard, value) || value is null)
            {
                return;
            }

            _ = value.OpenCommand.ExecuteAsync(null);
            SetProperty(ref _selectedCard, null);
        }
    }

    /// <summary>Whether nothing is saved.</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>Loads the saved musicians.</summary>
    public async Task LoadAsync()
    {
        var openWith = (await settings.LoadAsync()).OpenWith;
        Items.Clear();
        foreach (var musician in await saved.AllAsync())
        {
            Items.Add(cards.Create(musician, openWith, ExploreAsync));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    // Back to the one page, which searches it.
    private async Task ExploreAsync(SuggestionViewModel card)
    {
        await navigator.GoBackAsync();
        await flow.ShowAsync(card.Name, cached: false);
    }
}
