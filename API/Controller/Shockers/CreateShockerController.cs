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
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Shockers;

public sealed partial class ShockerController
{
    /// <summary>
    /// Register a shocker
    /// </summary>
    /// <response code="201">Successfully created shocker</response>
    /// <response code="400">You can have a maximum of 11 Shockers per Device.</response>
    /// <response code="404">Device does not exist</response>
    [HttpPost]
    [ProducesResponseType<BaseResponse<Guid>>(StatusCodes.Status201Created, MediaTypeNames.Application.Json)]
    [TokenPermission(PermissionType.Shockers_Edit)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // DeviceNotFound
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)] // TooManyShockers
    [MapToApiVersion("1")]
    public async Task<IActionResult> RegisterShocker(
        [FromBody] NewShocker body,
        [FromServices] IDeviceUpdateService deviceUpdateService)
    {
        var device = await _db.Devices.Where(x => x.Owner == CurrentUser.DbUser.Id && x.Id == body.Device)
            .Select(x => x.Id).FirstOrDefaultAsync();
        if (device == Guid.Empty) return Problem(DeviceError.DeviceNotFound);
        var shockerCount = await _db.Shockers.CountAsync(x => x.Device == body.Device);

        if (shockerCount >= 11) return Problem(DeviceError.TooManyShockers);

        var shocker = new Shocker
        {
            Id = Guid.NewGuid(),
            Name = body.Name,
            RfId = body.RfId,
            Device = body.Device,
            Model = body.Model
        };
        _db.Shockers.Add(shocker);
        await _db.SaveChangesAsync();

        await deviceUpdateService.UpdateDeviceForAllShared(CurrentUser.DbUser.Id, device,
            DeviceUpdateType.ShockerUpdated);
        
        return RespondSuccessLegacy(shocker.Id, statusCode: HttpStatusCode.Created);
    }
}