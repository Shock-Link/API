using System.Net;
using System.Net.Mime;
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
    /// Edit a shocker
    /// </summary>
    /// <param name="shockerId"></param>
    /// <param name="body"></param>
    /// <param name="deviceUpdateService"></param>
    /// <response code="200">Successfully updated shocker</response>
    /// <response code="404">Shocker does not exist</response>
    [HttpPatch("{shockerId}")]
    [TokenPermission(PermissionType.Shockers_Edit)]
    [ProducesResponseType<BaseResponse<object>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // DeviceNotFound, ShockerNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> EditShocker(
        [FromRoute] Guid shockerId,
        [FromBody] NewShocker body, 
        [FromServices] IDeviceUpdateService deviceUpdateService)
    {
        var device = await _db.Devices.AnyAsync(x => x.Owner == CurrentUser.DbUser.Id && x.Id == body.Device);
        if (!device) return Problem(DeviceError.DeviceNotFound);

        var shocker = await _db.Shockers.Where(x => x.DeviceNavigation.Owner == CurrentUser.DbUser.Id && x.Id == shockerId)
            .FirstOrDefaultAsync();
        if (shocker == null) return Problem(ShockerError.ShockerNotFound);
        var oldDevice = shocker.Device;

        shocker.Device = body.Device;
        shocker.Name = body.Name;
        shocker.RfId = body.RfId;
        shocker.Model = body.Model;

        await _db.SaveChangesAsync();
        
        if (oldDevice != body.Device) 
            await deviceUpdateService.UpdateDeviceForAllShared(CurrentUser.DbUser.Id, oldDevice, DeviceUpdateType.ShockerUpdated);
        
        await deviceUpdateService.UpdateDeviceForAllShared(CurrentUser.DbUser.Id, body.Device, DeviceUpdateType.ShockerUpdated);
        
        return RespondSuccessLegacySimple("Shocker updated successfully");
    }
}