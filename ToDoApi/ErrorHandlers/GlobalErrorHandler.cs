using Microsoft.AspNetCore.Mvc;

namespace ToDoApi.ErrorHandlers;

internal sealed class GlobalErrorHandler: Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
 
    public GlobalErrorHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }
 
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Title = "An error occurred while processing your request.",
                Detail = exception.Message,
                Type = exception.GetType().Name,
                Status = StatusCodes.Status500InternalServerError
            },
            Exception = exception
        });
    }
}