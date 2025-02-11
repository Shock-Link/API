using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using OpenShock.Common.Models;
using Asp.Versioning;
using OpenShock.API.Models.Requests;
using OpenShock.API.Services.Account;
using OpenShock.Common.DataAnnotations;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Services.Turnstile;
using OpenShock.Common.Utils;

namespace OpenShock.API.Controller.Account;

public sealed partial class AccountController
{
    /// <summary>
    /// Initiate a password reset
    /// </summary>
    /// <response code="200">Password reset email sent if the email is associated to an registered account</response>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.Json)]
    [MapToApiVersion("2")]
    public async Task<IActionResult> PasswordResetInitiateV2([FromBody] PasswordResetRequestV2 body, [FromServices] ICloudflareTurnstileService turnstileService, CancellationToken cancellationToken)
    {
        var turnStile = await turnstileService.VerifyUserResponseToken(body.TurnstileResponse, HttpContext.GetRemoteIP(), cancellationToken);
        if (!turnStile.IsT0)
        {
            var cfErrors = turnStile.AsT1.Value!;
            if (cfErrors.All(err => err == CloduflareTurnstileError.InvalidResponse))
                return Problem(TurnstileError.InvalidTurnstile);

            return Problem(new OpenShockProblem("InternalServerError", "Internal Server Error", HttpStatusCode.InternalServerError));
        }
        
        await _accountService.CreatePasswordReset(body.Email);
        
        return Ok();
    }
}