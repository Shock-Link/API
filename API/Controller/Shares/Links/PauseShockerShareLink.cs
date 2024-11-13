using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.API.Utils;
using OpenShock.Common.Models;
using System.Net;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Shares.Links;

public sealed partial class ShareLinksController
{
    /// <summary>
    /// Pause a shocker in a share link
    /// </summary>
    /// <param name="shareLinkId"></param>
    /// <param name="shockerId"></param>
    /// <param name="body"></param>
    /// <response code="200">Successfully updated paused state shocker</response>
    /// <response code="404">Share link or shocker does not exist</response>
    /// <response code="400">Shocker does not exist in share link</response>
    [HttpPost("{shareLinkId}/{shockerId}/pause")]
    [ProducesResponseType<BaseResponse<PauseReason>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShareLinkNotFound
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShockerNotInShareLink
    public async Task<IActionResult> PauseShocker([FromRoute] Guid shareLinkId, [FromRoute] Guid shockerId, [FromBody] PauseRequest body)
    {
        var exists = await _db.ShockerSharesLinks.AnyAsync(x => x.OwnerId == CurrentUser.DbUser.Id && x.Id == shareLinkId);
        if (!exists) return Problem(ShareLinkError.ShareLinkNotFound);

        var shocker =
            await _db.ShockerSharesLinksShockers.Where(x =>
                x.ShareLinkId == shareLinkId && x.ShockerId == shockerId).Include(x => x.Shocker).FirstOrDefaultAsync();
        if (shocker == null) return Problem(ShareLinkError.ShockerNotInShareLink);

        shocker.Paused = body.Pause;
        await _db.SaveChangesAsync();

        return RespondSuccessLegacy(ShareLinkUtils.GetPausedReason(shocker.Paused, shocker.Shocker.Paused));
    }
}