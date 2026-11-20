using System.Net.Http.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Messaging;

public sealed class RealtimeRelayOptions
{
    public const string SectionName = "RealtimeRelay";
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";
    public string InternalApiKey { get; set; } = "";
}

/// <summary>
/// El Worker no aloja el hub SignalR: entrega los eventos a la API mediante un endpoint interno
/// protegido por clave, y la API los difunde a los clientes. En producción puede reemplazarse por
/// un backplane (Redis o Azure SignalR) sin cambiar la capa de aplicación.
/// </summary>
public sealed class HttpRealtimeNotifier(HttpClient http, IOptions<RealtimeRelayOptions> options, ILogger<HttpRealtimeNotifier> logger)
    : IRealtimeNotifier
{
    public const string HeaderName = "X-Internal-Key";

    public async Task PublishAsync(RealtimeEvent realtimeEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/realtime")
            {
                Content = JsonContent.Create(realtimeEvent),
            };
            request.Headers.Add(HeaderName, options.Value.InternalApiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // La notificación en tiempo real es "best effort": el estado ya está persistido.
            logger.LogWarning(ex, "No se pudo notificar en tiempo real el evento {Type}.", realtimeEvent.Type);
        }
    }
}
