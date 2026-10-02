using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace SosMVP.Extensions;

public static class ExceptionHandlerExtensions
{
    public static WebApplication UseExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = async context =>
            {
                var exceptionHandler = context.RequestServices.GetRequiredService<ApiExceptionHandler>();

                await exceptionHandler.TryHandleAsync(context, ReadError(context), context.RequestAborted);
            }
        });

        return app;
    }

    public static IApplicationBuilder UseExceptionHandler(this IApplicationBuilder app, ApiExceptionHandler exceptionHandler)
    {
        return app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = async context =>
            {
                await exceptionHandler.TryHandleAsync(context, ReadError(context), context.RequestAborted);
            }
        });
    }

    private static Exception ReadError(HttpContext httpContext)
    {
        return httpContext.Features.Get<IExceptionHandlerFeature>()?.Error
            ?? throw new InvalidOperationException("No se encontró la excepción que produjo el error.");
    }
}

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        var statusCode = ResolveStatusCode(exception);

        _logger.LogError(exception, "Se produjo un error no controlado al procesar la petición: {StatusCode}", statusCode);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "text/plain; charset=utf-8";

        await httpContext.Response.WriteAsync(exception.Message, cancellationToken);

        return true;
    }

    private static int ResolveStatusCode(Exception exception)
    {
        return exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            DbUpdateException => StatusCodes.Status500InternalServerError,
            EntityException => StatusCodes.Status400BadRequest,
            HexArchApplicationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
    }
}
