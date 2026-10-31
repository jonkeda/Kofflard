namespace Kofflard.App.Services;

/// <summary>
/// Where the app reads its launch settings from on each head, as in Bruinet's Todo sample.
/// </summary>
/// <remarks>
/// <para><b>Windows:</b> environment variables, set on the app's process by the test driver.</para>
/// <para>
/// <b>Android:</b> string extras on the launch intent, which <c>MainActivity</c> hands over before the
/// first page is built. An environment variable is still read when there is no extra of that name.
/// </para>
/// </remarks>
public static class LaunchValues
{
    private static IReadOnlyDictionary<string, string> _extras = new Dictionary<string, string>();

    /// <summary>Uses the launch intent's extras. Call before the first page is created.</summary>
    public static void UseIntentExtras(IReadOnlyDictionary<string, string> extras) => _extras = extras;

    /// <summary>Reads one setting: the intent extra, else the environment variable, else null.</summary>
    public static string? Read(string name)
        => _extras.TryGetValue(name, out var value) ? value : Environment.GetEnvironmentVariable(name);
}
