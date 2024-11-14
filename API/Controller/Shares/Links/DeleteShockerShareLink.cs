using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mime;
using OpenShock.Common;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Models;

namespace OpenShock.API.Controller.Shares.Links;

public sealed partial class ShareLinksController
{
    /// <summary>
    /// Remove a shocker from a share link
    /// </summary>
    /// <param name="shareLinkId"></param>
    /// <param name="shockerId"></param>
    /// <response code="200">Successfully removed shocker</response>
    /// <response code="404">Share link or shocker does not exist</response>
    /// <response code="400">Shocker does not exist in share link</response>
    [HttpDelete("{shareLinkId}/{shockerId}")]
    [ProducesResponseType<BaseResponse<object>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShareLinkNotFound, ShockerNotInShareLink
    public async Task<IActionResult> RemoveShocker([FromRoute] Guid shareLinkId, [FromRoute] Guid shockerId)
    {
        var exists = await _db.ShockerSharesLinks
            .Where(x => x.Id == shareLinkId)
            .WhereIsUserOrAdmin(x => x.Owner, CurrentUser)
            .AnyAsync();
        if (!exists)
        {
            return Problem(ShareLinkError.ShareLinkNotFound);
        }

        var affected = await _db.ShockerSharesLinksShockers
            .Where(x => x.ShareLinkId == shareLinkId && x.ShockerId == shockerId)
            .ExecuteDeleteAsync();
        if (affected <= 0)
        {
            return Problem(ShareLinkError.ShockerNotInShareLink);
        }
        
        return RespondSuccessLegacySimple($"Successfully removed {affected} {(affected == 1 ? "shocker" : "shockers")}");
    }
}