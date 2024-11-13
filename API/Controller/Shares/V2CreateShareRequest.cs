using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.Common.Errors;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Problems;
using Z.EntityFramework.Plus;

namespace OpenShock.API.Controller.Shares;

public sealed partial class SharesController
{
    [HttpPost("requests")]
    [ProducesResponseType<Guid>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // UserNotFound
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status400BadRequest, "application/problem+json")] // ShareCreateCannotShareWithSelf
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // ShareCreateShockerNotFound
    [ApiVersion("2")]
    public async Task<IActionResult> CreateShare([FromBody] CreateShareRequest data)
    {
        if (data.User == CurrentUser.DbUser.Id)
        {
            return Problem(ShareError.ShareRequestCreateCannotShareWithSelf);
        }
        
        var providedShockerIds = data.Shockers.Select(x => x.Id).ToArray();
        var belongsToUsFuture = _db.Shockers.AsNoTracking().Where(x =>
            x.DeviceNavigation.Owner == CurrentUser.DbUser.Id && providedShockerIds.Contains(x.Id)).Select(x => x.Id).Future();
        
        if (data.User != null)
        {
            var existsFuture = _db.Users.AsNoTracking().DeferredAny(x => x.Id == data.User).FutureValue();
            
            // We can already resolve the futures here since this is the last future query
            if (!await existsFuture.ValueAsync()) return Problem(UserError.UserNotFound);
        }

        var belongsToUs = await belongsToUsFuture.ToArrayAsync();

        var missingShockers = providedShockerIds.Except(belongsToUs).ToArray();
        
        if (missingShockers.Length > 0)
        {
            return Problem(ShareError.ShareCreateShockerNotFound(missingShockers));
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var shareRequest = new ShareRequest
        {
            Id = Guid.NewGuid(),
            Owner = CurrentUser.DbUser.Id,
            User = data.User
        };
        _db.ShareRequests.Add(shareRequest);
        
        foreach (var createShockerShare in data.Shockers)
        {
            _db.ShareRequestsShockers.Add(new ShareRequestsShocker
            {
                ShareRequest = shareRequest.Id,
                Shocker = createShockerShare.Id,
                LimitDuration = createShockerShare.Limits.Duration,
                LimitIntensity = createShockerShare.Limits.Intensity,
                PermLive = createShockerShare.Permissions.Live,
                PermShock = createShockerShare.Permissions.Shock,
                PermSound = createShockerShare.Permissions.Sound,
                PermVibrate = createShockerShare.Permissions.Vibrate
            });
        }

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok(shareRequest.Id);
    }
}