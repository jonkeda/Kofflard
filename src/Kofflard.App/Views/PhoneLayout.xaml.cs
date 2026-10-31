using Kofflard.Core.ViewModels;

namespace Kofflard.App.Views;

public partial class PhoneLayout : ContentView
{
    public PhoneLayout()
    {
        InitializeComponent();
    }

    // Recent shows while the name field is focused and empty.
    private void OnNameFocused(object? sender, FocusEventArgs e)
    {
        if (BindingContext is DiscoverViewModel page)
        {
            page.IsEditing = true;
        }
    }

    private void OnNameUnfocused(object? sender, FocusEventArgs e)
    {
        if (BindingContext is DiscoverViewModel page)
        {
            page.IsEditing = false;
        }
    }
}
