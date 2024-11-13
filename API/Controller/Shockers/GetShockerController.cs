using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Response;
using OpenShock.Common.Errors;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Shockers;

public sealed partial class ShockerController
{
    /// <summary>
    /// Get information about a shocker.
    /// </summary>
    /// <param name="shockerId"></param>
    /// <response code="200">The shocker information was successfully retrieved.</response>
    /// <response code="404">The shocker does not exist or you do not have access to it.</response>
    [HttpGet("{shockerId}")]
    [ProducesResponseType<BaseResponse<ShockerWithDevice>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShockerNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> GetShockerById([FromRoute] Guid shockerId)
    {
        var shocker = await _db.Shockers.Where(x => x.DeviceNavigation.Owner == CurrentUser.DbUser.Id && x.Id == shockerId).Select(x => new ShockerWithDevice
        {
            Id = x.Id,
            Name = x.Name,
            RfId = x.RfId,
            CreatedOn = x.CreatedOn,
            Device = x.Device,
            Model = x.Model,
            IsPaused = x.Paused
        }).FirstOrDefaultAsync();

        if (shocker == null) return Problem(ShockerError.ShockerNotFound);
        return RespondSuccessLegacy(shocker);
    }
}