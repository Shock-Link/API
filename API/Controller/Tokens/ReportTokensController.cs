using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.Common.Authentication;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Services.Turnstile;
using OpenShock.Common.Utils;
using System.Net;

namespace OpenShock.API.Controller.Tokens;

public sealed partial class TokensController
{
    /// <summary>
    /// Endpoint to delete potentially compromised api tokens
    /// </summary>
    /// <param name="body"></param>
    /// <param name="turnstileService"></param>
    /// <param name="apiConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">The tokens were deleted if found</response>
    [HttpPost("report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReportTokens(
        [FromBody] ReportTokensRequest body,
        [FromServices] ICloudflareTurnstileService turnstileService,
        [FromServices] ApiConfig apiConfig,
        CancellationToken cancellationToken)
    {
        var remoteIP = HttpContext.GetRemoteIP();

        var turnStile = await turnstileService.VerifyUserResponseToken(body.TurnstileResponse, remoteIP, cancellationToken);
        if (!turnStile.IsT0)
        {
            var cfErrors = turnStile.AsT1.Value!;
            if (cfErrors.All(err => err == CloduflareTurnstileError.InvalidResponse))
                return Problem(TurnstileError.InvalidTurnstile);

            return Problem(new OpenShockProblem("InternalServerError", "Internal Server Error", HttpStatusCode.InternalServerError));
        }

        _db.ApiTokensReports.Add(new Common.OpenShockDb.ApiTokenReport {
            Id = Guid.CreateVersion7(),
            ReportedAt = DateTimeOffset.UtcNow,
            ReportedByUserId = CurrentUser.Id,
            ReportedByIp = remoteIP,
            ReportedByIpCountry = HttpContext.GetCFIPCountry(),
            ReportedByUser = CurrentUser
        });
        await _db.SaveChangesAsync(cancellationToken);

        var hashes = body.Secrets.Select(HashingUtils.HashSha256).ToArray();
        await _db.ApiTokens.Where(x => hashes.Contains(x.TokenHash)).ExecuteDeleteAsync(cancellationToken);

        // TODO: Push to webhook

        return Ok();
    }
}