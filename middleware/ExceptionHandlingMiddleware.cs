/*
 * File: ExceptionHandlingMiddleware.cs
 * Description: ASP.NET Core middleware to intercept exceptions and map them to HTTP status codes.
 * To Implement: Keep logging clean; format validation error objects as standard lists.
 */

using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ccms_backend.models;

namespace ccms_backend.middleware;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred in the request pipeline.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = exception switch
        {
            CaseNotFoundException => HttpStatusCode.NotFound,
            CaseAlreadyRespondedException => HttpStatusCode.Conflict,
            UnauthorisedActionException => HttpStatusCode.Forbidden,
            ValidationException => HttpStatusCode.BadRequest,
            InvalidOperationException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = (int)statusCode;

        object responseBody = exception switch
        {
            ValidationException validationEx => new
            {
                Message = "Validation failed",
                Errors = validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            },
            _ => new
            {
                Message = exception.Message
            }
        };

        var json = JsonSerializer.Serialize(responseBody);
        return context.Response.WriteAsync(json);
    }
}
