using FlowDesk.Application.Common;
using FlowDesk.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Infrastructure;

/// <summary>Traduce excepciones de aplicación y dominio a respuestas RFC 7807 (ProblemDetails).</summary>
public sealed class FlowDeskExceptionHandler(IProblemDetailsService problems, ILogger<FlowDeskExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, code) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Datos inválidos", "validation"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "No autenticado", "unauthorized"),
            ForbiddenException or DomainForbiddenException => (StatusCodes.Status403Forbidden, "Acceso denegado", "forbidden"),
            NotFoundException => (StatusCodes.Status404NotFound, "No encontrado", "not_found"),
            ConcurrencyException => (StatusCodes.Status409Conflict, "Conflicto de concurrencia", "concurrency"),
            DomainException d => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio", d.Code),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", "internal"),
        };

        if (status >= 500) logger.LogError(exception, "Error no controlado");
        else logger.LogInformation("Solicitud rechazada ({Status}): {Message}", status, exception.Message);

        var details = exception is ValidationException v
            ? new ValidationProblemDetails(v.Errors) { Title = title }
            : new ProblemDetails { Title = title };

        details.Status = status;
        details.Detail = status >= 500 ? "Ocurrió un error inesperado." : exception.Message;
        details.Extensions["code"] = code;
        details.Extensions["correlationId"] = context.Items[CorrelationIdMiddleware.ItemKey];

        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = details,
            Exception = exception,
        });
    }
}
