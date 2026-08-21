using System.Text.Json;
using DndCampaign.Api.Application.Invitations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DndCampaign.Api.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, problemDetails) = exception switch
        {
            InvitationConflictException => (
                StatusCodes.Status409Conflict,
                (object)new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Ya existe una invitación pendiente.",
                    Detail = "Revócala o utiliza la acción de reenvío cuando esté disponible.",
                }),
            InvitationStateException stateException => (
                StatusCodes.Status409Conflict,
                (object)new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "La invitación no puede reenviarse.",
                    Detail = stateException.Message,
                }),
            InvitationRateLimitException rateLimitException => (
                StatusCodes.Status429TooManyRequests,
                (object)new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "El reenvío está limitado temporalmente.",
                    Detail = $"Podrás volver a intentarlo a partir de {rateLimitException.RetryAt:O}.",
                    Extensions = { ["retryAt"] = rateLimitException.RetryAt },
                }),
            InvitationEmailValidationException emailValidationException => (
                StatusCodes.Status400BadRequest,
                (object)new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["email"] = [emailValidationException.Message],
                })
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                }),
            _ => (0, null!),
        };

        if (statusCode == 0)
        {
            return false;
        }

        logger.LogWarning(exception, "Handled API exception with status {StatusCode}", statusCode);
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json; charset=utf-8";
        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails),
            cancellationToken);
        return true;
    }
}
