using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlowDesk.Desktop.Capture;
using FlowDesk.Shared.Contracts;

namespace FlowDesk.Desktop.Services;

public sealed class ApiOptions
{
    public const string SectionName = "Api";
    public string BaseUrl { get; set; } = "http://localhost:5080";
}

/// <summary>Estado de la sesión del usuario en la aplicación de escritorio.</summary>
public sealed class SessionContext
{
    public string? AccessToken { get; private set; }
    public UserDto? User { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsAuthenticated => AccessToken is not null && ExpiresAt > DateTimeOffset.UtcNow;

    public bool Can(string permission) => User?.Permissions.Contains(permission) == true;

    public void Start(LoginResponse login)
    {
        AccessToken = login.AccessToken;
        User = login.User;
        ExpiresAt = login.ExpiresAt;
    }

    public void End()
    {
        AccessToken = null;
        User = null;
    }
}

public sealed class ApiException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>Cliente tipado de la API REST, usando los contratos compartidos de FlowDesk.Shared.</summary>
public sealed class FlowDeskApiClient(HttpClient http, SessionContext session)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct)
    {
        var response = await SendAsync(HttpMethod.Post, "api/auth/login", JsonContent.Create(new LoginRequest(email, password)), ct, authenticated: false);
        var login = await ReadAsync<LoginResponse>(response, ct);
        session.Start(login);
        return login;
    }

    public async Task<PagedResult<ExpedientSummaryDto>> SearchExpedientsAsync(string? search, CancellationToken ct)
    {
        // Solo estados que admiten documentos.
        var status = string.Join(',', ExpedientStatuses.Received, ExpedientStatuses.InValidation, ExpedientStatuses.CorrectionRequested);
        var url = $"api/expedients?status={status}&pageSize=50";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return await GetAsync<PagedResult<ExpedientSummaryDto>>(url, ct);
    }

    public Task<IReadOnlyList<UserLookupDto>> GetAssignableUsersAsync(CancellationToken ct) =>
        GetAsync<IReadOnlyList<UserLookupDto>>($"api/users/lookup?permission={FlowDeskPermissions.WorkflowValidate}", ct);

    public async Task<ExpedientDetailDto> CreateExpedientAsync(CreateExpedientRequest request, CancellationToken ct)
    {
        var response = await SendAsync(HttpMethod.Post, "api/expedients", JsonContent.Create(request), ct);
        return await ReadAsync<ExpedientDetailDto>(response, ct);
    }

    public Task<DocumentDto> GetDocumentAsync(Guid documentId, CancellationToken ct) =>
        GetAsync<DocumentDto>($"api/documents/{documentId}", ct);

    public async Task<DocumentDto> UploadDocumentAsync(
        Guid expedientId, CapturedDocument document, IProgress<double>? progress, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var file = new ProgressStreamContent(new MemoryStream(document.Content), progress);
        file.Headers.ContentType = new MediaTypeHeaderValue(document.ContentType);
        form.Add(file, "file", document.FileName);
        form.Add(new StringContent(document.Source), "source");

        var response = await SendAsync(HttpMethod.Post, $"api/expedients/{expedientId}/documents", form, ct);
        return await ReadAsync<DocumentDto>(response, ct);
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken ct) =>
        await ReadAsync<T>(await SendAsync(HttpMethod.Get, url, null, ct), ct);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, HttpContent? content, CancellationToken ct, bool authenticated = true)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (authenticated && session.AccessToken is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString("N"));

        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException($"No se pudo conectar con el servidor ({http.BaseAddress}). {ex.Message}");
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;

            throw new ApiException(await ExtractErrorAsync(response, ct), response.StatusCode);
        }
    }

    /// <summary>Convierte ProblemDetails (RFC 7807) en un mensaje legible.</summary>
    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized) return "Sesión inválida o credenciales incorrectas.";
        if (response.StatusCode == HttpStatusCode.Forbidden) return "No tiene permisos para esta operación.";
        if (response.StatusCode == HttpStatusCode.TooManyRequests) return "Demasiados intentos. Espere un momento.";

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray().Select(v => v.GetString()));
                return string.Join(" ", messages);
            }
            if (root.TryGetProperty("detail", out var detail)) return detail.GetString() ?? response.ReasonPhrase!;
        }
        catch (JsonException) { }

        return $"Error del servidor ({(int)response.StatusCode}).";
    }
}

/// <summary>Contenido HTTP que reporta el porcentaje enviado.</summary>
internal sealed class ProgressStreamContent(Stream content, IProgress<double>? progress) : HttpContent
{
    private const int BufferSize = 64 * 1024;

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var buffer = new byte[BufferSize];
        long sent = 0, total = content.Length;
        int read;
        while ((read = await content.ReadAsync(buffer)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read));
            sent += read;
            progress?.Report(total == 0 ? 1 : (double)sent / total);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = content.Length;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) content.Dispose();
        base.Dispose(disposing);
    }
}
