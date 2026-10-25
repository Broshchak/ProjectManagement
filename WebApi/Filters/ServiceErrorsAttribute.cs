using System.ComponentModel.DataAnnotations;
using Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApi.Filters;

/// <summary>Maps application errors without exposing provider exceptions or SQL.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ServiceErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        int? status = context.Exception switch
        {
            DataConflictException => StatusCodes.Status409Conflict,
            ValidationException => StatusCodes.Status400BadRequest,
            _ => null
        };
        if (!status.HasValue) return;
        context.Result = new ObjectResult(new { message = context.Exception.Message }) { StatusCode = status.Value };
        context.ExceptionHandled = true;
    }
}
