using System.Text.Json;
using Kofflard.Core.Ai;

namespace Kofflard.TestSupport;

/// <summary>
/// The real AI a test may use instead of <see cref="FakeAi"/>: Azure OpenAI / Foundry, from
/// <c>ai.secrets.json</c> at the repository root (git ignores it; <c>ai.secrets.example.json</c> shows the
/// shape) or the <c>KOFFLARD_LIVE_AI_*</c> variables, which override it.
/// </summary>
/// <remarks>
/// Nothing calls it unless <see cref="RequestedVariable"/> is set: <c>ai.secrets.json</c> is the dev app's
/// key, not the tests', so its being there is not a request. The key is passed to the app in memory, never
/// logged.
/// </remarks>
public sealed record LiveAi(AiConnection Connection, string Key)
{
    /// <summary>The secrets file's name, at the repository root.</summary>
    public const string FileName = "ai.secrets.json";

    /// <summary>Set to <c>1</c> to run the Live integration tests, or the UI and UAT suites, against the live AI.</summary>
    public const string RequestedVariable = "KOFFLARD_LIVE_AI";

    /// <summary>The endpoint, overriding the file.</summary>
    public const string EndpointVariable = "KOFFLARD_LIVE_AI_ENDPOINT";

    /// <summary>The deployment, overriding the file.</summary>
    public const string DeploymentVariable = "KOFFLARD_LIVE_AI_DEPLOYMENT";

    /// <summary>The key, overriding the file.</summary>
    public const string KeyVariable = "KOFFLARD_LIVE_AI_KEY";

    /// <summary>Whether this run asked for the live AI in the UI suites.</summary>
    public static bool Requested => Environment.GetEnvironmentVariable(RequestedVariable) == "1";

    /// <summary>The live AI, or null when no endpoint, deployment and key are set.</summary>
    public static LiveAi? TryLoad()
    {
        string? endpoint = null, deployment = null, key = null;
        if (FindFile() is { } file)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            if (json.RootElement.TryGetProperty("azure", out var azure))
            {
                endpoint = Read(azure, "endpoint");
                deployment = Read(azure, "deployment");
                key = Read(azure, "key");
            }
        }

        endpoint = Environment.GetEnvironmentVariable(EndpointVariable) ?? endpoint;
        deployment = Environment.GetEnvironmentVariable(DeploymentVariable) ?? deployment;
        key = Environment.GetEnvironmentVariable(KeyVariable) ?? key;

        return string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(key)
            ? null
            : new LiveAi(new AiConnection(AiProviders.Azure.Id, endpoint, deployment), key);
    }

    /// <summary>The live AI; throws, saying what to set, when there is none.</summary>
    public static LiveAi Load()
        => TryLoad() ?? throw new InvalidOperationException(
            $"{RequestedVariable}=1 needs the live AI: copy ai.secrets.example.json to {FileName} at the repository root, or set {EndpointVariable}, {DeploymentVariable} and {KeyVariable}.");

    /// <inheritdoc />
    /// <remarks>Never the key.</remarks>
    public override string ToString() => $"LiveAi {{ {Connection.Describe()} at {Connection.Endpoint} }}";

    private static string? Read(JsonElement section, string name)
        => section.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // From the test's output folder up to the folder that holds Kofflard.slnx.
    private static string? FindFile()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Kofflard.slnx")))
            {
                var file = Path.Combine(folder.FullName, FileName);
                return File.Exists(file) ? file : null;
            }
        }

        return null;
    }
}

/// <summary>
/// A fact that needs the live AI: skipped, saying why, unless the run asks for it (<see cref="LiveAi.Requested"/>)
/// and <see cref="LiveAi.TryLoad"/> finds one.
/// </summary>
public sealed class LiveAiFactAttribute : Xunit.FactAttribute
{
    /// <summary>Skips the test unless the live AI was asked for and is set up.</summary>
    public LiveAiFactAttribute()
    {
        if (!LiveAi.Requested)
        {
            Skip = $"Calls the real AI: set {LiveAi.RequestedVariable}=1 to run it.";
        }
        else if (LiveAi.TryLoad() is null)
        {
            Skip = $"No live AI: copy ai.secrets.example.json to {LiveAi.FileName} at the repository root.";
        }
    }
}
