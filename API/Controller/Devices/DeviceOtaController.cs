using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.Common.Authentication.Attributes;
using OpenShock.Common.Errors;
using OpenShock.Common.Models;
using OpenShock.Common.Models.Services.Ota;
using OpenShock.Common.Problems;
using OpenShock.Common.Services.Ota;

namespace OpenShock.API.Controller.Devices;

public sealed partial class DevicesController
{
    /// <summary>
    /// Gets the OTA update history for a device
    /// </summary>
    /// <param name="deviceId">Id of the device</param>
    /// <param name="otaService"></param>
    /// <response code="200">OK</response>
    /// <response code="404">Could not find device or you do not have access to it</response>
    [HttpGet("{deviceId}/ota")]
    [UserSessionOnly]
    [ProducesResponseType<BaseResponse<IReadOnlyCollection<OtaItem>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // DeviceNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> GetOtaUpdateHistory([FromRoute] Guid deviceId, [FromServices] IOtaService otaService)
    {
        // Check if user owns device or has a share
        var deviceExistsAndYouHaveAccess = await _db.Devices.AnyAsync(x =>
            x.Id == deviceId && x.Owner == CurrentUser.DbUser.Id);
        if (!deviceExistsAndYouHaveAccess) return Problem(DeviceError.DeviceNotFound);

        return RespondSuccessLegacy(await otaService.GetUpdates(deviceId));
    }
    
}