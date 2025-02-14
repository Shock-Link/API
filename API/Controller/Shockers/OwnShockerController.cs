using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Response;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Shockers;

public sealed partial class ShockerController
{
    /// <summary>
    /// List all shockers belonging to the authenticated user.
    /// </summary>
    /// <response code="200">The shockers were successfully retrieved.</response>
    [HttpGet("own")]
    [ProducesResponseType<BaseResponse<IAsyncEnumerable<ResponseDeviceWithShockers>>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [MapToApiVersion("1")]
    public async Task<IActionResult> ListShockers()
    {
        var shockers = _db.Devices
            .Where(x => x.Owner == CurrentUser.Id)
            .OrderBy(x => x.CreatedOn).Select(x => new ResponseDeviceWithShockers
            {
                Id = x.Id,
                Name = x.Name,
                CreatedOn = x.CreatedOn,
                Shockers = x.Shockers
                    .OrderBy(y => y.CreatedOn)
                    .Select(y => new ShockerResponse
                    {
                        Id = y.Id,
                        Name = y.Name,
                        RfId = y.RfId,
                        CreatedOn = y.CreatedOn,
                        Model = y.Model,
                        IsPaused = y.Paused
                    })
                    .ToArray()
            })
            .AsAsyncEnumerable();

        return RespondSuccessLegacy(shockers);
    }
}