using System.Collections.ObjectModel;
using Bruinet.Samples.Shared.ViewModels;
using Kofflard.Core.Models;
using Kofflard.Core.Services;

namespace Kofflard.Core.ViewModels;

/// <summary>The favourites, with the same card as the results.</summary>
public sealed class SavedViewModel(ISavedStore saved, ISettingsStore settings, SuggestionCards cards, INavigator navigator) : ParentViewModel
{
    /// <summary>The saved musicians, newest first.</summary>
    public ObservableCollection<SuggestionViewModel> Items { get; } = [];

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

    private async Task ExploreAsync(SuggestionViewModel card)
    {
        var level = (await settings.LoadAsync()).Adventure;
        await navigator.GoToAsync(KofflardRoutes.Results, KofflardRoutes.ForSearch(card.Name, new Adventure(level)));
    }
}
