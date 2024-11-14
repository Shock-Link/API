using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using OpenShock.API.Models.Requests;
using OpenShock.Common.Models;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Shares.Links;

public sealed partial class ShareLinksController
{
    /// <summary>
    /// Create a new share link
    /// </summary>
    /// <response code="200">The created share link</response>
    [HttpPost(Name = "CreateShareLink")]
    [ProducesResponseType<BaseResponse<Guid>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<IActionResult> CreateShareLink([FromBody] ShareLinkCreate body)
    {
        var entity = new ShockerSharesLink
        {
            Id = Guid.NewGuid(),
            Owner = CurrentUser.DbUser,
            ExpiresOn = body.ExpiresOn == null ? null : DateTime.SpecifyKind(body.ExpiresOn.Value, DateTimeKind.Utc),
            Name = body.Name
        };
        _db.ShockerSharesLinks.Add(entity);
        await _db.SaveChangesAsync();

        return RespondSuccessLegacy(entity.Id);
    }
}