using System.Text.Json;
using LMS.Business.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LMS.WebApi.Middleware;

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
        catch (AppException ex)
        {
            await WriteErrorAsync(context, ex.StatusCode, ex.Message, ex.Errors);
        }
        catch (DbUpdateConcurrencyException)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict,
                "The record was modified concurrently by another transaction. Please refresh and try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled server error");
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while processing your request.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string message,
        Dictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = statusCode,
            message,
            errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
