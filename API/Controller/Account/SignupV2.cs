using Microsoft.AspNetCore.Mvc;
using OpenShock.API.Models.Requests;
using System.Net;
using System.Net.Mime;
using Asp.Versioning;
using OpenShock.API.Services.Account;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Services.Turnstile;
using OpenShock.Common.Utils;
using OpenShock.Common.Models;

namespace OpenShock.API.Controller.Account;

public sealed partial class AccountController
{
    /// <summary>
    /// Signs up a new user
    /// </summary>
    /// <param name="body"></param>
    /// <param name="turnstileService"></param>
    /// <param name="apiConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">User successfully signed up</response>
    /// <response code="400">Username or email already exists</response>
    [HttpPost("signup")]
    [ProducesResponseType<BaseResponse<object>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)] // EmailOrUsernameAlreadyExists
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)] // InvalidTurnstileResponse
    [MapToApiVersion("2")]
    public async Task<IActionResult> SignUpV2(
        [FromBody] SignUpV2 body,
        [FromServices] ICloudflareTurnstileService turnstileService,
        [FromServices] ApiConfig apiConfig,
        CancellationToken cancellationToken)
    {
        if (apiConfig.Turnstile.Enabled)
        {
            var turnStile = await turnstileService.VerifyUserResponseToken(body.TurnstileResponse, HttpContext.GetRemoteIP(), cancellationToken);
            if (!turnStile.IsT0) return Problem(TurnstileError.InvalidTurnstile);
        }

        var creationAction = await _accountService.Signup(body.Email, body.Username, body.Password);
        if (creationAction.IsT1)
            return Problem(SignupError.EmailAlreadyExists);

        return RespondSuccessLegacySimple("Successfully signed up");
    }
}