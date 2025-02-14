using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using OpenShock.Common.Errors;

namespace OpenShock.Common.Authentication;

public class OpenShockAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly JsonSerializerOptions _serializerOptions;

    private readonly AuthorizationMiddlewareResultHandler
        _defaultHandler = new AuthorizationMiddlewareResultHandler();

    public OpenShockAuthorizationMiddlewareResultHandler(IOptions<JsonOptions> jsonOptions)
    {
        _serializerOptions = jsonOptions.Value.SerializerOptions;
    }
    
    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            var failedRequirements = authorizeResult.AuthorizationFailure?.FailedRequirements.Select(x => x.ToString() ?? "error").ToArray() ?? [];
            var problem = AuthorizationError.PolicyNotMet(failedRequirements);
            context.Response.StatusCode = problem.Status!.Value;
            problem.AddContext(context);
            return context.Response.WriteAsJsonAsync(problem, _serializerOptions, contentType: MediaTypeNames.Application.ProblemJson);
        }
        
        return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}