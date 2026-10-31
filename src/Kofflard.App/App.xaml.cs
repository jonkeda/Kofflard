namespace Kofflard.App;

public partial class App : Application
{
    /// <summary>
    /// The native window title. Automation attaches to the app by this string.
    /// </summary>
    public const string WindowTitle = "Kofflard";

    /// <summary>The window's width on Windows: wide, for the desktop layout (.my/design/one-page-desktop.md).</summary>
    public const double WindowWidth = 1280;

    /// <summary>The window's height on Windows.</summary>
    public const double WindowHeight = 800;

    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;

        // On Android the page shrinks above the keyboard, so the phone's dock sits on it
        // (.my/design/single-page-phone.md). iOS scrolls the focused field into view by itself.
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application.SetWindowSoftInputModeAdjust(
            this,
            Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
    }

    /// <summary>
    /// Creates the window, titled explicitly so a driver attaching by title finds it. On Windows it starts
    /// wide, unless the launch asks for a size (<c>KOFFLARD_WINDOW=1280x800</c>), so a recorded video
    /// has the same frame every take.
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell) { Title = WindowTitle };
        if (Services.LaunchValues.Read(Infrastructure.LaunchSettings.WindowVariable) is { } size
            && size.Split('x') is [var width, var height]
            && double.TryParse(width, System.Globalization.CultureInfo.InvariantCulture, out var w)
            && double.TryParse(height, System.Globalization.CultureInfo.InvariantCulture, out var h))
        {
            window.Width = w;
            window.Height = h;
        }
#if WINDOWS
        else
        {
            window.Width = WindowWidth;
            window.Height = WindowHeight;
        }
#endif

        return window;
    }
}
