using System.ClientModel;
using System.ClientModel.Primitives;
using Kofflard.Core.Ai;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Kofflard.Infrastructure.Ai;

/// <summary>
/// Builds an <see cref="IChatClient"/> for a connection (design §6.4). Every provider in the catalogue
/// speaks the OpenAI-compatible chat API, so this is the OpenAI SDK with the provider's base URL.
/// </summary>
/// <remarks>
/// Azure goes through its v1 API (<c>…/openai/v1/</c>), which takes the same requests; its key is sent
/// as <c>api-key</c> besides the bearer header. The model is Azure's deployment name.
/// </remarks>
public sealed class ChatClientFactory(HttpClient http, TimeSpan timeout)
{
    /// <summary>A client for <paramref name="connection"/>.</summary>
    /// <param name="connection">Which provider, where, which model.</param>
    /// <param name="secret">The key or token; null for a provider that needs none.</param>
    public IChatClient Create(AiConnection connection, string? secret)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = connection.BaseUrl,
            NetworkTimeout = timeout,
            // One retry is ours (DiscoveryEngine); the SDK's own would multiply it and the wait.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            Transport = new HttpClientPipelineTransport(http),
            UserAgentApplicationId = "Kofflard",
        };

        if (connection.ProviderId == AiProviders.Azure.Id && !string.IsNullOrEmpty(secret))
        {
            options.AddPolicy(new HeaderPolicy("api-key", secret), PipelinePosition.PerCall);
        }

        // A local server takes any key; the SDK insists on one.
        var credential = new ApiKeyCredential(string.IsNullOrEmpty(secret) ? "none" : secret);
        return new OpenAIClient(credential, options).GetChatClient(connection.Model).AsIChatClient();
    }

    private sealed class HeaderPolicy(string name, string value) : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            message.Request.Headers.Set(name, value);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            message.Request.Headers.Set(name, value);
            return ProcessNextAsync(message, pipeline, currentIndex);
        }
    }
}
