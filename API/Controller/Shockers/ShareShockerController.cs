using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.API.Models.Response;
using OpenShock.API.Services;
using OpenShock.Common.Models;
using OpenShock.Common.OpenShockDb;
using System.Net;
using System.Net.Mime;
using OpenShock.Common.Authentication.Attributes;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Utils;

namespace OpenShock.API.Controller.Shockers;

public sealed partial class ShockerController
{
    /// <summary>
    /// Get all shares for a shocker
    /// </summary>
    /// <param name="shockerId">Id of the shocker</param>
    /// <response code="200">OK</response>
    /// <response code="404">The shocker does not exist or you do not have access to it.</response>
    [HttpGet("{shockerId}/shares")]
    [ProducesResponseType<BaseResponse<IEnumerable<ShareInfo>>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShockerNotFound    
    [MapToApiVersion("1")]
    public async Task<IActionResult> GetShockerShares([FromRoute] Guid shockerId)
    {
        var owns = await _db.Shockers.AnyAsync(x => x.DeviceNavigation.Owner == CurrentUser.DbUser.Id && x.Id == shockerId);
        if (!owns) return Problem(ShockerError.ShockerNotFound);
        var shares = await _db.ShockerShares
            .Where(x => x.ShockerId == shockerId && x.Shocker.DeviceNavigation.Owner == CurrentUser.DbUser.Id).Select(x =>
                new ShareInfo
                {
                    Paused = x.Paused,
                    SharedWith = new GenericIni
                    {
                        Name = x.SharedWithNavigation.Name,
                        Id = x.SharedWith,
                        Image = GravatarUtils.GetImageUrl(x.SharedWithNavigation.Email)
                    },
                    CreatedOn = x.CreatedOn,
                    Permissions = new ShockerPermissions
                    {
                        Sound = x.PermSound,
                        Shock = x.PermShock,
                        Vibrate = x.PermVibrate,
                        Live = x.PermLive
                    },
                    Limits = new ShockerLimits
                    {
                        Duration = x.LimitDuration,
                        Intensity = x.LimitIntensity
                    }
                }
            ).ToListAsync();

        return RespondSuccessLegacy(shares);
    }

    /// <summary>
    /// List all share codes for a shocker
    /// </summary>
    /// <param name="shockerId"></param>
    /// <response code="200">OK</response>
    [HttpGet("{shockerId}/shareCodes")]
    [ProducesResponseType<BaseResponse<IEnumerable<ShareCodeInfo>>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShockerNotFound    
    [MapToApiVersion("1")]
    public async Task<IActionResult> ShockerShareCodeList([FromRoute] Guid shockerId)
    {
        var owns = await _db.Shockers.AnyAsync(x => x.DeviceNavigation.Owner == CurrentUser.DbUser.Id && x.Id == shockerId);
        if (!owns) return Problem(ShockerError.ShockerNotFound);
        var shares = await _db.ShockerShareCodes
            .Where(x => x.ShockerId == shockerId && x.Shocker.DeviceNavigation.Owner == CurrentUser.DbUser.Id).Select(x =>
                new ShareCodeInfo
                {
                    CreatedOn = x.CreatedOn,
                    Id = x.Id
                }
            ).ToListAsync();

        return RespondSuccessLegacy(shares);
    }

    public sealed class ShareCodeInfo
    {
        public required Guid Id { get; set; }
        public required DateTime CreatedOn { get; set; }
    }

    /// <summary>
    /// Create a share code for a shocker
    /// </summary>
    /// <param name="shockerId"></param>
    /// <param name="body"></param>
    /// <param name="deviceUpdateService"></param>
    /// <response code="200">The share code was successfully created.</response>
    /// <response code="404">Shocker does not exists or you do not have access to it.</response>
    /// <returns></returns>
    [HttpPost("{shockerId}/shares")]
    [TokenPermission(PermissionType.Shockers_Edit)]
    [ProducesResponseType<BaseResponse<Guid>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShockerNotFound    
    [MapToApiVersion("1")]
    public async Task<IActionResult> ShockerShareCodeCreate(
        [FromRoute] Guid shockerId,
        [FromBody] ShockerPermLimitPair body,
        [FromServices] IDeviceUpdateService deviceUpdateService
    )
    {
        var device = await _db.Shockers.Where(x => x.DeviceNavigation.Owner == CurrentUser.DbUser.Id && x.Id == shockerId)
            .Select(x => x.Device).FirstOrDefaultAsync();
        if (device == Guid.Empty) return Problem(ShockerError.ShockerNotFound);

        var newCode = new ShockerShareCode
        {
            Id = Guid.NewGuid(),
            ShockerId = shockerId,
            PermVibrate = body.Permissions.Vibrate,
            PermSound = body.Permissions.Sound,
            PermShock = body.Permissions.Shock,
            LimitIntensity = body.Limits.Intensity,
            LimitDuration = body.Limits.Duration
        };
        _db.ShockerShareCodes.Add(newCode);
        await _db.SaveChangesAsync();

        await deviceUpdateService.UpdateDeviceForAllShared(CurrentUser.DbUser.Id, device, DeviceUpdateType.ShockerUpdated);

        return RespondSuccessLegacy(newCode.Id);
    }

    /// <summary>
    /// Remove a share code for a shocker
    /// </summary>
    /// <param name="shockerId"></param>
    /// <param name="sharedWithUserId"></param>
    /// <param name="deviceUpdateService"></param>
    /// <response code="200">Successfully removed share code</response>
    /// <response code="404">Share does not exists or device/shocker does not belong to you nor is shared with you</response>
    [HttpDelete("{shockerId}/shares/{sharedWithUserId}")]
    [TokenPermission(PermissionType.Shockers_Edit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShockerNotFound    
    [MapToApiVersion("1")]
    public async Task<IActionResult> ShockerShareCodeRemove(
        [FromRoute] Guid shockerId,
        [FromRoute] Guid sharedWithUserId,
        [FromServices] IDeviceUpdateService deviceUpdateService)
    {
        var affected = await _db.ShockerShares.Where(x =>
                x.ShockerId == shockerId && x.SharedWith == sharedWithUserId &&
                (x.Shocker.DeviceNavigation.Owner == CurrentUser.DbUser.Id || x.SharedWith == CurrentUser.DbUser.Id))
            .ExecuteDeleteAsync();
        if (affected <= 0) return Problem(ShockerError.ShockerNotFound);

        var device = await _db.Shockers.Where(x => x.Id == shockerId)
            .Select(x => new { x.Device, x.DeviceNavigation.Owner }).SingleAsync();

        await deviceUpdateService.UpdateDevice(device.Owner, device.Device, DeviceUpdateType.ShockerUpdated, sharedWithUserId);

        return Ok();
    }

    /// <summary>
    /// Update a share for a shocker
    /// </summary>
    /// <param name="shockerId"></param>
    /// <param name="sharedWithUserId"></param>
    /// <param name="body"></param>
    /// <param name="deviceUpdateService"></param>
    /// <response code="200">Successfully updated share code</response>
    /// <response code="404">The share code does not exist or you do not have access to it.</response>
    [HttpPatch("{shockerId}/shares/{sharedWithUserId}")]
    [TokenPermission(PermissionType.Shockers_Edit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShockerNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> ShockerShareCodeUpdate(
        [FromRoute] Guid shockerId,
        [FromRoute] Guid sharedWithUserId,
        [FromBody] ShockerPermLimitPair body,
        [FromServices] IDeviceUpdateService deviceUpdateService)
    {
        var affected = await _db.ShockerShares.Where(x =>
                x.ShockerId == shockerId && x.SharedWith == sharedWithUserId &&
                x.Shocker.DeviceNavigation.Owner == CurrentUser.DbUser.Id).Select(x =>
                new { Share = x, DeviceId = x.Shocker.Device, Owner = x.Shocker.DeviceNavigation.Owner })
            .FirstOrDefaultAsync();
        if (affected == null) return Problem(ShockerError.ShockerNotFound);

        var share = affected.Share;
        
        share.PermShock = body.Permissions.Shock;
        share.PermSound = body.Permissions.Sound;
        share.PermVibrate = body.Permissions.Vibrate;
        share.LimitDuration = body.Limits.Duration;
        share.LimitIntensity = body.Limits.Intensity;
        share.PermLive = body.Permissions.Live;

        await _db.SaveChangesAsync();

        await deviceUpdateService.UpdateDevice(affected.Owner, affected.DeviceId, DeviceUpdateType.ShockerUpdated, sharedWithUserId);

        return Ok();
    }

    /// <summary>
    /// Pause/Unpause a share code for a shocker
    /// </summary>
    /// <param name="shockerId"></param>
    /// <param name="sharedWithUserId"></param>
    /// <param name="body"></param>
    /// <param name="deviceUpdateService"></param>
    /// <response code="200">Successfully updated pause status share</response>
    /// <response code="404">The share code does not exist or you do not have access to it.</response>
    [HttpPost("{shockerId}/shares/{sharedWithUserId}/pause")]
    [TokenPermission(PermissionType.Shockers_Pause)]
    [ProducesResponseType<BaseResponse<bool>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShockerNotFound    
    [MapToApiVersion("1")]
    public async Task<IActionResult> ShockerShareCodePause(
        [FromRoute] Guid shockerId,
        [FromRoute] Guid sharedWithUserId,
        [FromBody] PauseRequest body,
        [FromServices] IDeviceUpdateService deviceUpdateService)
    {
        var affected = await _db.ShockerShares.Where(x =>
            x.ShockerId == shockerId && x.SharedWith == sharedWithUserId &&
            x.Shocker.DeviceNavigation.Owner == CurrentUser.DbUser.Id).Select(x =>
            new { Share = x, DeviceId = x.Shocker.Device, Owner = x.Shocker.DeviceNavigation.Owner }).FirstOrDefaultAsync();
        if (affected == null) return Problem(ShockerError.ShockerNotFound);

        affected.Share.Paused = body.Pause;

        await _db.SaveChangesAsync();

        await deviceUpdateService.UpdateDevice(affected.Owner, affected.DeviceId, DeviceUpdateType.ShockerUpdated, sharedWithUserId);

        return RespondSuccessLegacy(body.Pause);
    }
}