namespace Kofflard.Core.Ai;

/// <summary>How the app gets access to a provider.</summary>
public enum AiAuth
{
    /// <summary>The user pastes an API key.</summary>
    ApiKey,

    /// <summary>The user signs in (OAuth with PKCE) and the provider hands the app a key; pasting one works too.</summary>
    SignInOrApiKey,

    /// <summary>No secret: a local server such as Ollama or LM Studio.</summary>
    None,
}

/// <summary>How the provider takes the answer's shape.</summary>
public enum AiStructuredOutput
{
    /// <summary>A JSON schema in the request (<c>response_format: json_schema</c>).</summary>
    JsonSchema,

    /// <summary>The schema only in the prompt; the answer is parsed and validated as usual.</summary>
    PromptOnly,
}

/// <summary>
/// One AI provider the app can talk to (design §6.1). Every provider here speaks the OpenAI-compatible
/// chat API, so adding one is a catalogue entry, not code.
/// </summary>
/// <param name="Id">The stable id kept in settings.</param>
/// <param name="Name">The name the user reads.</param>
/// <param name="Auth">How access is given.</param>
/// <param name="BaseUrl">The OpenAI-compatible base URL, or null when the user gives an endpoint.</param>
/// <param name="DefaultModel">The model (Azure: the deployment) a new connection starts with.</param>
/// <param name="StructuredOutput">How the answer's shape is asked for.</param>
/// <param name="KeyPage">Where the user gets a key, or null.</param>
/// <param name="EndpointHint">For providers that need an endpoint: an example.</param>
public sealed record AiProvider(
    string Id,
    string Name,
    AiAuth Auth,
    string? BaseUrl,
    string DefaultModel,
    AiStructuredOutput StructuredOutput,
    string? KeyPage,
    string? EndpointHint = null)
{
    /// <summary>Whether the user must give an endpoint (Azure, a local server).</summary>
    public bool NeedsEndpoint => BaseUrl is null;

    /// <summary>Whether a secret is needed.</summary>
    public bool NeedsSecret => Auth != AiAuth.None;

    /// <summary>Whether a sign-in button is offered.</summary>
    public bool CanSignIn => Auth == AiAuth.SignInOrApiKey;
}

/// <summary>The providers, Azure first (design §3.3, decided 2026-10-31).</summary>
public static class AiProviders
{
    /// <summary>Azure OpenAI / Foundry: the first and default provider.</summary>
    public static AiProvider Azure { get; } = new(
        "azure",
        "Azure OpenAI / Foundry",
        AiAuth.ApiKey,
        null,
        "gpt-4.1-mini",
        AiStructuredOutput.JsonSchema,
        "https://ai.azure.com",
        "https://<resource>.openai.azure.com");

    /// <summary>OpenRouter: sign in with OAuth, or paste a key.</summary>
    public static AiProvider OpenRouter { get; } = new(
        "openrouter",
        "OpenRouter",
        AiAuth.SignInOrApiKey,
        "https://openrouter.ai/api/v1/",
        "openai/gpt-4.1-mini",
        AiStructuredOutput.JsonSchema,
        "https://openrouter.ai/settings/keys");

    /// <summary>Every provider, in the order the Connect page lists them.</summary>
    public static IReadOnlyList<AiProvider> All { get; } =
    [
        Azure,
        OpenRouter,
        new("openai", "OpenAI", AiAuth.ApiKey, "https://api.openai.com/v1/", "gpt-4.1-mini", AiStructuredOutput.JsonSchema, "https://platform.openai.com/api-keys"),
        new("anthropic", "Anthropic (Claude)", AiAuth.ApiKey, "https://api.anthropic.com/v1/", "claude-haiku-5-5", AiStructuredOutput.PromptOnly, "https://console.anthropic.com/settings/keys"),
        new("gemini", "Google Gemini", AiAuth.ApiKey, "https://generativelanguage.googleapis.com/v1beta/openai/", "gemini-2.5-flash", AiStructuredOutput.JsonSchema, "https://aistudio.google.com/apikey"),
        new("mistral", "Mistral", AiAuth.ApiKey, "https://api.mistral.ai/v1/", "mistral-small-latest", AiStructuredOutput.JsonSchema, "https://console.mistral.ai/api-keys"),
        new("huggingface", "Hugging Face", AiAuth.ApiKey, "https://router.huggingface.co/v1/", "meta-llama/Llama-3.3-70B-Instruct", AiStructuredOutput.PromptOnly, "https://huggingface.co/settings/tokens"),
        new("local", "Local (Ollama / LM Studio)", AiAuth.None, null, "llama3.1", AiStructuredOutput.JsonSchema, null, "http://localhost:11434"),
    ];

    /// <summary>The provider with this id, or Azure when the id is unknown.</summary>
    public static AiProvider Find(string? id) => All.FirstOrDefault(provider => provider.Id == id) ?? Azure;
}

/// <summary>
/// A connection the user set up: which provider, where, which model. The secret is kept apart, in
/// secure storage (<see cref="ISecretStore"/>).
/// </summary>
/// <param name="ProviderId">The provider's id.</param>
/// <param name="Endpoint">The endpoint, for providers that need one; else null.</param>
/// <param name="Model">The model, or for Azure the deployment.</param>
public sealed record AiConnection(string ProviderId, string? Endpoint, string Model)
{
    /// <summary>The provider.</summary>
    public AiProvider Provider => AiProviders.Find(ProviderId);

    /// <summary>
    /// The OpenAI-compatible base URL: the provider's own, or one made from the endpoint. Azure's
    /// v1 API lives under <c>/openai/v1/</c>; a local server's under <c>/v1/</c>.
    /// </summary>
    public Uri BaseUrl
    {
        get
        {
            if (Provider.BaseUrl is { } fixedUrl)
            {
                return new Uri(fixedUrl);
            }

            var endpoint = (Endpoint ?? string.Empty).Trim().TrimEnd('/');
            if (endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(endpoint + "/");
            }

            return Provider.Id == AiProviders.Azure.Id
                ? new Uri(endpoint.EndsWith("/openai", StringComparison.OrdinalIgnoreCase) ? endpoint + "/v1/" : endpoint + "/openai/v1/")
                : new Uri(endpoint + "/v1/");
        }
    }

    /// <summary>"Azure OpenAI / Foundry · gpt-4.1-mini".</summary>
    public string Describe() => $"{Provider.Name} · {Model}";
}
