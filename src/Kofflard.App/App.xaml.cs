namespace Kofflard.App;

public partial class App : Application
{
    /// <summary>
    /// The native window title. Automation attaches to the app by this string.
    /// </summary>
    public const string WindowTitle = "Kofflard";

    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    /// <summary>Creates the window, titled explicitly so a driver attaching by title finds it.</summary>
    protected override Window CreateWindow(IActivationState? activationState)
        => new(_shell) { Title = WindowTitle };
}
