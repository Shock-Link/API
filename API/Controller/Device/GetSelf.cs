using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Response;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;

namespace OpenShock.API.Controller.Device;

public sealed partial class DeviceController
{
    /// <summary>
    /// Gets information about the authenticated device.
    /// </summary>
    /// <response code="200">The device information was successfully retrieved.</response>
    [HttpGet("self")]
    [ProducesResponseType<BaseResponse<DeviceSelfResponse>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetSelf()
    {
        var shockers = await _db.Shockers.Where(x => x.Device == CurrentDevice.Id).Select(x => new MinimalShocker
        {
            Id = x.Id,
            RfId = x.RfId,
            Model = x.Model
        }).ToArrayAsync();

        return RespondSuccessLegacy(new DeviceSelfResponse
            {
                Id = CurrentDevice.Id,
                Name = CurrentDevice.Name,
                Shockers = shockers
            }
        );
    }
}