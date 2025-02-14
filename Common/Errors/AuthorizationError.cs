using System.Net;
using Microsoft.AspNetCore.Authorization;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;
using OpenShock.Common.Problems.CustomProblems;

namespace OpenShock.Common.Errors;

public static class AuthorizationError
{
    public static OpenShockProblem UnknownError => new("Authorization.UnknownError", "An unknown error occurred.",
        HttpStatusCode.InternalServerError);

    public static OpenShockProblem UserSessionOnly => new("Authorization.UserSession.Only",
        "This endpoint is only available to use with a user sessions", HttpStatusCode.Forbidden);
    
    public static OpenShockProblem TokenOnly => new("Authorization.Token.Only",
        "This endpoint is only available to use with api tokens", HttpStatusCode.Forbidden);

    public static TokenPermissionProblem TokenPermissionMissing(PermissionType requiredPermission,
        List<PermissionType> grantedPermissions) => new("Authorization.Token.PermissionMissing",
        $"You do not have the required permission to access this endpoint. Missing permission: {requiredPermission.ToString()}",
        requiredPermission, grantedPermissions, HttpStatusCode.Forbidden);
    
    public static PolicyNotMetProblem PolicyNotMet(string[] failedRequirements) => new PolicyNotMetProblem(failedRequirements);
}