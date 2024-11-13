using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.API.Services;
using OpenShock.Common;
using OpenShock.Common.Authentication.Attributes;
using OpenShock.Common.Errors;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;
using OpenShock.Common.Redis;
using OpenShock.Common.Utils;
using System.Linq.Expressions;
using System.Net;

namespace OpenShock.API.Controller.Devices;

public sealed partial class DevicesController
{
    /// <summary>
    /// Get all devices for the current user
    /// </summary>
    /// <response code="200">All devices for the current user</response>
    [HttpGet]
    [ProducesResponseType<BaseResponse<IEnumerable<Models.Response.ResponseDevice>>>(StatusCodes.Status200OK)]
    [MapToApiVersion("1")]
    public async Task<IActionResult> ListDevices()
    {
        var devices = await _db.Devices.Where(x => x.Owner == CurrentUser.DbUser.Id)
            .Select(x => new Models.Response.ResponseDevice
            {
                Id = x.Id,
                Name = x.Name,
                CreatedOn = x.CreatedOn
            }).ToListAsync();

        return RespondSuccessLegacy(devices);
    }

    /// <summary>
    /// Get a device by its id
    /// </summary>
    /// <param name="deviceId"></param>
    /// <response code="200">The device</response>
    [HttpGet("{deviceId}")]
    [ProducesResponseType<BaseResponse<Models.Response.ResponseDeviceWithToken>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // DeviceNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> GetDeviceById([FromRoute] Guid deviceId)
    {
        var hasAuthPerms = IsAllowed(PermissionType.Devices_Auth);
        
        
        var device = await _db.Devices.Where(x => x.Owner == CurrentUser.DbUser.Id && x.Id == deviceId)
            .Select(x => new Models.Response.ResponseDeviceWithToken
            {
                Id = x.Id,
                Name = x.Name,
                CreatedOn = x.CreatedOn,
                Token = hasAuthPerms ? x.Token : null
            }).FirstOrDefaultAsync();
        if (device == null) return Problem(DeviceError.DeviceNotFound);

        return RespondSuccessLegacy(device);
    }

    /// <summary>
    /// Edit a device
    /// </summary>
    /// <param name="deviceId"></param>
    /// <param name="body"></param>
    /// <param name="updateService"></param>
    /// <response code="200">Successfully updated device</response>
    /// <response code="404">Device does not exist</response>
    [HttpPatch("{deviceId}")]
    [TokenPermission(PermissionType.Devices_Edit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // DeviceNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> EditDevice([FromRoute] Guid deviceId, [FromBody] HubEditRequest body, [FromServices] IDeviceUpdateService updateService)
    {
        var device = await _db.Devices.Where(x => x.Owner == CurrentUser.DbUser.Id && x.Id == deviceId)
            .FirstOrDefaultAsync();
        if (device == null) return Problem(DeviceError.DeviceNotFound);

        device.Name = body.Name;
        await _db.SaveChangesAsync();

        await updateService.UpdateDeviceForAllShared(CurrentUser.DbUser.Id, device.Id, DeviceUpdateType.Updated);

        return Ok();
    }

    /// <summary>
    /// Regenerate a device token
    /// </summary>
    /// <param name="deviceId">The id of the device to regenerate the token for</param>
    /// <response code="200">Successfully regenerated device token</response>
    /// <response code="404">Device does not exist</response>
    /// <response code="500">Failed to save regenerated token</response>
    [HttpPut("{deviceId}")]
    [TokenPermission(PermissionType.Devices_Edit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // DeviceNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> RegenerateDeviceToken([FromRoute] Guid deviceId)
    {
        var device = await _db.Devices.Where(x => x.Owner == CurrentUser.DbUser.Id && x.Id == deviceId)
            .FirstOrDefaultAsync();
        if (device == null) return Problem(DeviceError.DeviceNotFound);

        device.Token = CryptoUtils.RandomString(256);

        var affected = await _db.SaveChangesAsync();
        if (affected <= 0) throw new Exception("Failed to save regenerated token");

        return Ok();
    }

    /// <summary>
    /// Remove a device from current user's account
    /// </summary>
    /// <param name="deviceId">The id of the device to delete</param>
    /// <param name="updateService"></param>
    /// <response code="200">Successfully deleted device</response>
    /// <response code="404">Device does not exist</response>
    [HttpDelete("{deviceId}")]
    [TokenPermission(PermissionType.Devices_Edit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // DeviceNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> RemoveDevice([FromRoute] Guid deviceId, [FromServices] IDeviceUpdateService updateService)
    {
        var affected = await _db.Devices.Where(x => x.Id == deviceId).WhereIsUserOrAdmin(x => x.OwnerNavigation, CurrentUser).ExecuteDeleteAsync();
        if (affected <= 0) return Problem(DeviceError.DeviceNotFound);
        
        await updateService.UpdateDeviceForAllShared(CurrentUser.DbUser.Id, deviceId, DeviceUpdateType.Deleted);
        
        return Ok();
    }

    /// <summary>
    /// Create a new device for the current user
    /// </summary>
    /// <response code="201">Successfully created device</response>
    [HttpPost]
    [TokenPermission(PermissionType.Devices_Edit)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [MapToApiVersion("1")]
    public Task<Guid> CreateDevice([FromServices] IDeviceUpdateService updateService)
    => CreateDeviceV2(new HubCreateRequest
        {
            Name = $"New Hub {DateTimeOffset.UtcNow:d}"
        }, updateService);
    
    
    /// <summary>
    /// Create a new device for the current user
    /// </summary>
    /// <response code="201">Successfully created device</response>
    [HttpPost]
    [TokenPermission(PermissionType.Devices_Edit)]
    [ProducesResponseType<Guid>(StatusCodes.Status201Created)]
    [MapToApiVersion("2")]
    public async Task<Guid> CreateDeviceV2([FromBody] HubCreateRequest data, [FromServices] IDeviceUpdateService updateService)
    {
        var device = new Common.OpenShockDb.Device
        {
            Id = Guid.NewGuid(),
            Owner = CurrentUser.DbUser.Id,
            Name = data.Name,
            Token = CryptoUtils.RandomString(256)
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        
        await updateService.UpdateDevice(CurrentUser.DbUser.Id, device.Id, DeviceUpdateType.Created);

        Response.StatusCode = (int)HttpStatusCode.Created;
        return device.Id;
    }

    /// <summary>
    /// Get a pair code for a device
    /// </summary>
    /// <param name="deviceId"></param>
    /// <response code="200">The pair code</response>
    /// <response code="404">Device does not exist or does not belong to you</response>
    [HttpGet("{deviceId}/pair")]
    [TokenPermission(PermissionType.Devices_Edit)]
    [ProducesResponseType<BaseResponse<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // DeviceNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> GetPairCode([FromRoute] Guid deviceId)
    {
        var devicePairs = _redis.RedisCollection<DevicePair>();

        var deviceExists = await _db.Devices.AnyAsync(x => x.Id == deviceId && x.Owner == CurrentUser.DbUser.Id);
        if (!deviceExists) Problem(DeviceError.DeviceNotFound);
        // replace with unlink?
        var existing = await devicePairs.FindByIdAsync(deviceId.ToString());
        if (existing != null) await devicePairs.DeleteAsync(existing);

        string pairCode = CryptoUtils.RandomNumericString(6);

        var devicePairDto = new DevicePair
        {
            Id = deviceId,
            PairCode = pairCode
        };
        await devicePairs.InsertAsync(devicePairDto, TimeSpan.FromMinutes(15));

        return RespondSuccessLegacy(pairCode);
    }

    /// <summary>
    /// Get LCG info for a device if it is online and connected to a LCG node
    /// </summary>
    /// <param name="deviceId"></param>
    /// <response code="200">LCG node was found and device is online</response>
    /// <response code="404">Device does not exist or does not belong to you</response>
    /// <response code="404">Device is not online</response>
    /// <response code="412">Device is online but not connected to a LCG node, you might need to upgrade your firmware to use this feature</response>
    /// <response code="500">Internal server error, lcg node could not be found</response>
    [HttpGet("{deviceId}/lcg")]
    [ProducesResponseType<BaseResponse<LcgResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // DeviceNotFound
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // DeviceIsNotOnline
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status412PreconditionFailed, "application/problem+json")] // DeviceNotConnectedToGateway
    [MapToApiVersion("1")]
    public async Task<IActionResult> GetLiveControlGatewayInfo([FromRoute] Guid deviceId)
    {
        // Check if user owns device or has a share
        var deviceExistsAndYouHaveAccess = await _db.Devices.AnyAsync(x =>
            x.Id == deviceId && (x.Owner == CurrentUser.DbUser.Id || x.Shockers.Any(y => y.ShockerShares.Any(
                z => z.SharedWith == CurrentUser.DbUser.Id))));
        if (!deviceExistsAndYouHaveAccess) return Problem(DeviceError.DeviceNotFound);

        // Check if device is online
        var devicesOnline = _redis.RedisCollection<DeviceOnline>();
        var online = await devicesOnline.FindByIdAsync(deviceId.ToString());
        if (online == null) return Problem(DeviceError.DeviceIsNotOnline);

        // Check if device is connected to a LCG node
        if (online.Gateway == null) return Problem(DeviceError.DeviceNotConnectedToGateway);

        // Get LCG node info
        var lcgNodes = _redis.RedisCollection<LcgNode>();
        var gateway = await lcgNodes.FindByIdAsync(online.Gateway);
        if (gateway == null) throw new Exception("Internal server error, lcg node could not be found");

        return RespondSuccessLegacy(new LcgResponse
        {
            Gateway = gateway.Fqdn,
            Country = gateway.Country
        });
    }
}