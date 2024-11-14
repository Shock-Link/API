using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using OpenShock.API.Models.Response;
using OpenShock.Common.Authentication.Attributes;
using OpenShock.Common.Authentication.Services;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Tokens;

public sealed partial class TokensController
{
    /// <summary>
    /// Gets information about the current token used to access this endpoint
    /// </summary>
    /// <param name="userReferenceService"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    [HttpGet("self")]
    [TokenOnly]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public TokenResponse GetSelfToken([FromServices] IUserReferenceService userReferenceService)
    {
        var x = userReferenceService.AuthReference;
        
        if (x == null) throw new Exception("This should not be reachable due to AuthenticatedSession requirement");
        if (!x.Value.IsT1) throw new Exception("This should not be reachable due to the [TokenOnly] attribute");
        
        var token = x.Value.AsT1;
        
        return new TokenResponse
        {
            CreatedOn = token.CreatedOn,
            ValidUntil = token.ValidUntil,
            LastUsed = token.LastUsed,
            Permissions = token.Permissions,
            Name = token.Name,
            Id = token.Id
        };
    }
}