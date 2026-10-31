namespace Kofflard.App.Views;

public partial class DesktopLayout : ContentView
{
    public DesktopLayout()
    {
        InitializeComponent();
    }
}

/// <summary>The artist map's boxes, as Core gives them, to MAUI's.</summary>
public sealed class MapBoundsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is Kofflard.Core.ViewModels.Rect box ? new Rect(box.X, box.Y, box.Width, box.Height) : Rect.Zero;

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
