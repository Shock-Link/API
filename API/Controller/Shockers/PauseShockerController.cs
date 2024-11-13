using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.API.Services;
using OpenShock.Common.Authentication.Attributes;
using OpenShock.Common.Errors;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Shockers;

public sealed partial class ShockerController
{
    /// <summary>
    /// Pause or unpause a shocker
    /// </summary>
    /// <param name="shockerId"></param>
    /// <param name="body"></param>
    /// <param name="deviceUpdateService"></param>
    /// <response code="200">Successfully set pause state</response>
    /// <response code="404">Shocker not found or does not belong to you</response>
    [HttpPost("{shockerId}/pause")]
    [TokenPermission(PermissionType.Shockers_Pause)]
    [ProducesResponseType<BaseResponse<bool?>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShockerNotFound    
    [MapToApiVersion("1")]
    public async Task<IActionResult> PauseShocker([FromRoute] Guid shockerId, [FromBody] PauseRequest body,
        [FromServices] IDeviceUpdateService deviceUpdateService)
    {
        var shocker = await _db.Shockers.Where(x => x.Id == shockerId && x.DeviceNavigation.Owner == CurrentUser.DbUser.Id)
            .FirstOrDefaultAsync();
        if (shocker == null) return Problem(ShockerError.ShockerNotFound);
        shocker.Paused = body.Pause;
        await _db.SaveChangesAsync();

        await deviceUpdateService.UpdateDeviceForAllShared(CurrentUser.DbUser.Id, shocker.Device, DeviceUpdateType.ShockerUpdated);

        return RespondSuccessLegacy(body.Pause);
    }
}