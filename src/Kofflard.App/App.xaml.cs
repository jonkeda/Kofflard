namespace Kofflard.App;

public partial class App : Application
{
    /// <summary>
    /// The native window title. Automation attaches to the app by this string.
    /// </summary>
    public const string WindowTitle = "Kofflard";

    /// <summary>The window's width on Windows: a phone's, so the app looks as it does on one.</summary>
    public const double PhoneWidth = 420;

    /// <summary>The window's height on Windows.</summary>
    public const double PhoneHeight = 860;

    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    /// <summary>
    /// Creates the window, titled explicitly so a driver attaching by title finds it. On Windows it starts
    /// phone-sized, unless the launch asks for a size (<c>KOFFLARD_WINDOW=1280x800</c>), so a recorded video
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
            window.Width = PhoneWidth;
            window.Height = PhoneHeight;
        }
#endif

        return window;
    }
}
