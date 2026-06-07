using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RetriEval.Llm.Abstractions;

/// <summary>
/// An <see cref="ILlmClient"/> implementation for OpenAI-compatible chat completion APIs,
/// with zero extra runtime dependencies — uses only <see cref="System.Net.Http.HttpClient"/>
/// and <see cref="System.Text.Json"/> from the .NET base class library.
/// </summary>
/// <remarks>
/// <para>
/// Works with OpenAI, Azure OpenAI, Ollama, LM Studio, and any endpoint that speaks the
/// OpenAI <c>/chat/completions</c> request format.
/// </para>
/// <para>
/// <b>OpenAI example:</b>
/// <code>
/// var llm = new OpenAILlmClient(
///     apiKey: Environment.GetEnvironmentVariable("OPENAI_KEY")!);
/// var grader = new LlmJudgeGrader(llm);
/// </code>
/// </para>
/// <para>
/// <b>Azure OpenAI (resource-key auth) example:</b>
/// <code>
/// var llm = new OpenAILlmClient(
///     apiKey: Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")!,
///     endpoint: "https://{resource}.openai.azure.com/openai/deployments/{deployment}" +
///               "/chat/completions?api-version=2024-02-01",
///     authHeaderName: "api-key");
/// </code>
/// </para>
/// <para>
/// <b>Ollama (local) example:</b>
/// <code>
/// var llm = new OpenAILlmClient(
///     apiKey: "ollama",
///     model: "llama3",
///     endpoint: "http://localhost:11434/v1/chat/completions");
/// </code>
/// </para>
/// <para>
/// Dispose the client when finished to release the owned <see cref="HttpClient"/> (if one
/// was not supplied via the <c>httpClient</c> constructor parameter).
/// </para>
/// </remarks>
public sealed class OpenAILlmClient : ILlmClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly double _temperature;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates a client targeting an OpenAI-compatible chat completions endpoint.
    /// </summary>
    /// <param name="apiKey">
    /// API key. By default sent as <c>Authorization: Bearer {apiKey}</c>.
    /// Pass <c>authHeaderName: "api-key"</c> for Azure OpenAI resource-key authentication.
    /// </param>
    /// <param name="model">
    /// Model identifier sent in the request body (e.g. <c>gpt-4o-mini</c>, <c>gpt-4o</c>).
    /// Ignored by Azure OpenAI (the deployment URL already encodes the model).
    /// </param>
    /// <param name="endpoint">Full URL of the chat completions endpoint.</param>
    /// <param name="authHeaderName">
    /// Name of the HTTP authentication header. Use <c>"api-key"</c> for Azure OpenAI resource-key
    /// authentication; defaults to <c>"Authorization"</c> (Bearer scheme).
    /// </param>
    /// <param name="temperature">
    /// Sampling temperature sent to the model. Defaults to <c>0</c> for deterministic output,
    /// which is recommended when using this client with <see cref="LlmJudgeGrader"/>.
    /// </param>
    /// <param name="httpClient">
    /// Optional pre-configured <see cref="HttpClient"/>. When provided, the caller owns the
    /// client's lifetime and this instance will not dispose it. When <see langword="null"/>,
    /// a new <see cref="HttpClient"/> is created and disposed with this instance.
    /// </param>
    public OpenAILlmClient(
        string apiKey,
        string model = "gpt-4o-mini",
        string endpoint = "https://api.openai.com/v1/chat/completions",
        string authHeaderName = "Authorization",
        double temperature = 0,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(authHeaderName);

        _endpoint = endpoint;
        _model = model;
        _temperature = temperature;
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient();

        if (authHeaderName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        else
            _http.DefaultRequestHeaders.TryAddWithoutValidation(authHeaderName, apiKey);
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var payload = JsonSerializer.Serialize(new
        {
            model = _model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 512,
            temperature = _temperature,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
}
