using System.ComponentModel.DataAnnotations;
using Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Infrastructure
{
    /// <summary>Maps known exceptions to 4xx; everything else becomes a generic 500 with no internals leaked.</summary>
    public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IProblemDetailsService problemDetails)
        : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
        {
            var (status, title) = exception switch
            {
                ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
                ConflictException => (StatusCodes.Status409Conflict, "Conflict."),
                ServiceBusyException => (StatusCodes.Status503ServiceUnavailable, "The server is busy. Try again shortly."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception");
            }

            httpContext.Response.StatusCode = status;

            if (status == StatusCodes.Status503ServiceUnavailable)
            {
                httpContext.Response.Headers.RetryAfter = "5";
            }

            return await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title,
                    Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
                }
            });
        }
    }
}
