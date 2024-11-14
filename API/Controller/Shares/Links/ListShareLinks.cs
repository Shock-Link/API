using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Response;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Shares.Links;

public sealed partial class ShareLinksController
{
    /// <summary>
    /// Get all share links for the current user
    /// </summary>
    /// <response code="200">All share links for the current user</response>
    [HttpGet]
    [ProducesResponseType<BaseResponse<IEnumerable<ShareLinkResponse>>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<IActionResult> List()
    {
        var ownShareLinks = await _db.ShockerSharesLinks.Where(x => x.OwnerId == CurrentUser.DbUser.Id)
            .Select(x => ShareLinkResponse.GetFromEf(x)).ToListAsync();

        return RespondSuccessLegacy(ownShareLinks);
    }
}