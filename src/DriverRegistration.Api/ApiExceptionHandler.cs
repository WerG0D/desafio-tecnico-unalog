using Microsoft.AspNetCore.Diagnostics;

namespace DriverRegistration.Api;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception is BadHttpRequestException badRequest
            ? badRequest.StatusCode : StatusCodes.Status500InternalServerError;

        if (statusCode >= 500)
        {
            logger.LogError("Falha no cadastro. Tipo: {ExceptionType}. Rastreio: {TraceId}.",
                exception.GetType().Name, httpContext.TraceIdentifier);
        }

        await ApiProblems.ForStatus(statusCode, httpContext.TraceIdentifier).ExecuteAsync(httpContext);
        return true;
    }
}
