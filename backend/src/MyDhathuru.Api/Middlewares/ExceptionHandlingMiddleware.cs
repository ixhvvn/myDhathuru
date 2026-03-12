using System.Net;
using System.Text.Json;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            AppException appException =>
            (
                (int)appException.StatusCode,
                ApiResponse<object>.Fail(appException.Message, appException.Data["Errors"] as IEnumerable<string>)
            ),
            _ =>
            (
                (int)HttpStatusCode.InternalServerError,
                ApiResponse<object>.Fail("An unexpected error occurred.")
            )
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception.");
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
