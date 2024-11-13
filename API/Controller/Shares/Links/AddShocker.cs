using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.Common.OpenShockDb;
using System.Net;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Models;

namespace OpenShock.API.Controller.Shares.Links;

public sealed partial class ShareLinksController
{
    /// <summary>
    /// Add a shocker to a share link
    /// </summary>
    /// <param name="shareLinkId"></param>
    /// <param name="shockerId"></param>
    /// <response code="200">Successfully added shocker</response>
    /// <response code="404">Share link or shocker does not exist</response>
    /// <response code="409">Shocker already exists in share link</response>
    [HttpPost("{shareLinkId}/{shockerId}")]
    [ProducesResponseType<BaseResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShareLinkNotFound
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShockerNotFound
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status409Conflict, "application/problem+json")] // ShockerAlreadyInShareLink
    public async Task<IActionResult> AddShocker([FromRoute] Guid shareLinkId, [FromRoute] Guid shockerId)
    {
        var exists = await _db.ShockerSharesLinks.AnyAsync(x => x.OwnerId == CurrentUser.DbUser.Id && x.Id == shareLinkId);
        if (!exists) return Problem(ShareLinkError.ShareLinkNotFound);

        var ownShocker =
            await _db.Shockers.AnyAsync(x => x.Id == shockerId && x.DeviceNavigation.Owner == CurrentUser.DbUser.Id);
        if (!ownShocker) return Problem(ShockerError.ShockerNotFound);

        if (await _db.ShockerSharesLinksShockers.AnyAsync(x => x.ShareLinkId == shareLinkId && x.ShockerId == shockerId))
            return Problem(ShareLinkError.ShockerAlreadyInShareLink);

        _db.ShockerSharesLinksShockers.Add(new ShockerSharesLinksShocker
        {
            ShockerId = shockerId,
            ShareLinkId = shareLinkId,
            PermSound = true,
            PermVibrate = true,
            PermShock = true
        });

        await _db.SaveChangesAsync();
        
        return RespondSuccessLegacySimple("Successfully added shocker");
    }
}