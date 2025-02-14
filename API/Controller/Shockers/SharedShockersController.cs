using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Response;
using OpenShock.Common.Models;
using OpenShock.Common.Utils;
using System.Net.Mime;

namespace OpenShock.API.Controller.Shockers;

file record OwnerGroupKey(Guid OwnerId, string OwnerName, string OwnerEmail)
{
    public override int GetHashCode() => OwnerId.GetHashCode();
}

file record DeviceGroupKey(Guid DeviceId, string DeviceName)
{
    public override int GetHashCode() => DeviceId.GetHashCode();
}

public sealed partial class ShockerController
{
    /// <summary>
    /// List all shockers shared with the authenticated user.
    /// </summary>
    /// <response code="200">The shockers were successfully retrieved.</response>
    [HttpGet("shared")]
    [ProducesResponseType<BaseResponse<IEnumerable<OwnerShockerResponse>>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [MapToApiVersion("1")]
    public async Task<IActionResult> ListSharedShockers()
    {
        var sharedShockersData = await _db.ShockerShares
            .AsNoTracking()
            .Include(x => x.Shocker.DeviceNavigation.OwnerNavigation)
            .Where(x => x.SharedWith == CurrentUser.Id)
            .Select(x => new
            {
                OwnerId = x.Shocker.DeviceNavigation.OwnerNavigation.Id,
                OwnerName = x.Shocker.DeviceNavigation.OwnerNavigation.Name,
                OwnerEmail = x.Shocker.DeviceNavigation.OwnerNavigation.Email,
                DeviceId = x.Shocker.DeviceNavigation.Id,
                DeviceName = x.Shocker.DeviceNavigation.Name,
                Shocker = new OwnerShockerResponse.SharedDevice.SharedShocker
                {
                    Id = x.Shocker.Id,
                    Name = x.Shocker.Name,
                    IsPaused = x.Shocker.Paused,
                    Permissions = new ShockerPermissions
                    {
                        Shock = x.PermShock,
                        Sound = x.PermSound,
                        Vibrate = x.PermVibrate,
                        Live = x.PermLive
                    },
                    Limits = new ShockerLimits
                    {
                        Duration = x.LimitDuration,
                        Intensity = x.LimitIntensity
                    }
                }
            })
            .ToArrayAsync();

        var sharesResponse = sharedShockersData
            .GroupBy(x => new OwnerGroupKey(x.OwnerId, x.OwnerName, x.OwnerEmail))
            .Select(ownerGroup => new OwnerShockerResponse
            {
                Id = ownerGroup.Key.OwnerId,
                Name = ownerGroup.Key.OwnerName,
                Image = GravatarUtils.GetUserImageUrl(ownerGroup.Key.OwnerEmail),
                Devices = ownerGroup
                    .GroupBy(x => new DeviceGroupKey(x.DeviceId, x.DeviceName))
                    .Select(deviceGroup => new OwnerShockerResponse.SharedDevice
                    {
                        Id = deviceGroup.Key.DeviceId,
                        Name = deviceGroup.Key.DeviceName,
                        Shockers = deviceGroup
                            .Select(x => x.Shocker)
                            .ToArray(),
                    })
                    .ToArray()
            });

        return RespondSuccessLegacy(sharesResponse);
    }
}