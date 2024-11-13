using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.API.Models.Response;
using System.Net;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Models;

namespace OpenShock.API.Controller.Shares.Links;

public sealed partial class ShareLinksController
{
    /// <summary>
    /// Edit a shocker in a share link
    /// </summary>
    /// <param name="shareLinkId"></param>
    /// <param name="shockerId"></param>
    /// <param name="body"></param>
    /// <response code="200">Successfully updated shocker</response>
    /// <response code="404">Share link or shocker does not exist</response>
    /// <response code="400">Shocker does not exist in share link</response>
    [HttpPatch("{shareLinkId}/{shockerId}")]
    [ProducesResponseType<BaseResponse<ShareLinkResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShareLinkNotFound
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShockerNotInShareLink
    public async Task<IActionResult> EditShocker([FromRoute] Guid shareLinkId, [FromRoute] Guid shockerId, [FromBody] ShareLinkEditShocker body)
    {
        var exists = await _db.ShockerSharesLinks.AnyAsync(x => x.OwnerId == CurrentUser.DbUser.Id && x.Id == shareLinkId);
        if (!exists) return Problem(ShareLinkError.ShareLinkNotFound);

        var shocker =
            await _db.ShockerSharesLinksShockers.FirstOrDefaultAsync(x =>
                x.ShareLinkId == shareLinkId && x.ShockerId == shockerId);
        if (shocker == null) return Problem(ShareLinkError.ShockerNotInShareLink);

        shocker.PermSound = body.Permissions.Sound;
        shocker.PermVibrate = body.Permissions.Vibrate;
        shocker.PermShock = body.Permissions.Shock;
        shocker.LimitDuration = body.Limits.Duration;
        shocker.LimitIntensity = body.Limits.Intensity;
        shocker.PermLive = body.Permissions.Live;
        shocker.Cooldown = body.Cooldown;

        await _db.SaveChangesAsync();
        return RespondSuccessLegacySimple("Successfully updated shocker");
    }
}