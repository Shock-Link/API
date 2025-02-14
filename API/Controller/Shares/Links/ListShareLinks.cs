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
    [ProducesResponseType<BaseResponse<IAsyncEnumerable<ShareLinkResponse>>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public IActionResult List()
    {
        var ownShareLinks = _db.ShockerSharesLinks
            .Where(x => x.OwnerId == CurrentUser.Id)
            .Select(x => ShareLinkResponse.GetFromEf(x))
            .AsAsyncEnumerable();

        return RespondSuccessLegacy(ownShareLinks);
    }
}