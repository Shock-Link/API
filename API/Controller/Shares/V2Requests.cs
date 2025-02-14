using System.Net;
using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.API.Models.Response;
using OpenShock.Common.Errors;
using OpenShock.Common.Extensions;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;
using OpenShock.Common.Utils;

namespace OpenShock.API.Controller.Shares;

public sealed partial class SharesController
{
    [HttpGet("requests/outstanding")]
    [ProducesResponseType<IAsyncEnumerable<ShareRequestBaseItem>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ApiVersion("2")]
    public IAsyncEnumerable<ShareRequestBaseItem> GetOutstandingRequestsList()
    {
        return _db.ShareRequests
            .Where(x => x.Owner == CurrentUser.Id)
            .Select(x => new ShareRequestBaseItem()
            {
                Id = x.Id,
                CreatedOn = x.CreatedOn,
                Owner = new GenericIni
                {
                    Id = x.OwnerNavigation.Id,
                    Name = x.OwnerNavigation.Name,
                    Image = x.OwnerNavigation.GetImageUrl()
                },
                SharedWith = x.UserNavigation == null
                    ? null
                    : new GenericIni
                    {
                        Id = x.UserNavigation.Id,
                        Name = x.UserNavigation.Name,
                        Image = x.UserNavigation.GetImageUrl()
                    },
                Counts = new ShareRequestBaseItem.ShareRequestCounts
                {
                    Shockers = x.ShareRequestsShockers.Count
                }
            })
            .AsAsyncEnumerable();
    }
    
    [HttpGet("requests/incoming")]
    [ProducesResponseType<IAsyncEnumerable<ShareRequestBaseItem>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ApiVersion("2")]
    public IAsyncEnumerable<ShareRequestBaseItem> GetIncomingRequestsList()
    {
        return _db.ShareRequests
            .Where(x => x.User == CurrentUser.Id)
            .Select(x => new ShareRequestBaseItem
            {
                Id = x.Id,
                CreatedOn = x.CreatedOn,
                Owner = new GenericIni
                {
                    Id = x.OwnerNavigation.Id,
                    Name = x.OwnerNavigation.Name,
                    Image = x.OwnerNavigation.GetImageUrl()
                },
                SharedWith = x.UserNavigation == null
                    ? null
                    : new GenericIni
                    {
                        Id = x.UserNavigation.Id,
                        Name = x.UserNavigation.Name,
                        Image = x.UserNavigation.GetImageUrl()
                    },
                Counts = new ShareRequestBaseItem.ShareRequestCounts
                {
                    Shockers = x.ShareRequestsShockers.Count
                }
            })
            .AsAsyncEnumerable();
    }
    
    [HttpGet("requests/{id:guid}")]
    [ProducesResponseType<ShareRequestBaseDetails>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShareRequestNotFound
    [ApiVersion("2")]
    public async Task<IActionResult> GetRequest(Guid id)
    {
        var outstandingShare = await _db.ShareRequests.Where(x => x.Id == id && (x.Owner == CurrentUser.Id || x.User == CurrentUser.Id))
            .Select(x => new ShareRequestBaseDetails()
            {
                Id = x.Id,
                CreatedOn = x.CreatedOn,
                Owner = new GenericIni
                {
                    Id = x.OwnerNavigation.Id,
                    Name = x.OwnerNavigation.Name,
                    Image = x.OwnerNavigation.GetImageUrl()
                },
                SharedWith = x.UserNavigation == null
                    ? null
                    : new GenericIni
                    {
                        Id = x.UserNavigation.Id,
                        Name = x.UserNavigation.Name,
                        Image = x.UserNavigation.GetImageUrl()
                    },
                Shockers = x.ShareRequestsShockers.Select(y => new ShockerPermLimitPairWithId
                {
                    Id = y.Shocker,
                    Limits = new ShockerLimits
                    {
                        Duration = y.LimitDuration,
                        Intensity = y.LimitIntensity
                    },
                    Permissions = new ShockerPermissions
                    {
                        Shock = y.PermShock,
                        Sound = y.PermSound,
                        Vibrate = y.PermVibrate,
                        Live = y.PermLive
                    }
                }).ToArray()
            }).FirstOrDefaultAsync();
        
        if (outstandingShare == null) return Problem(ShareError.ShareRequestNotFound);
        
        return Ok(outstandingShare);
    }
    
    [HttpDelete("requests/outgoing/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShareRequestNotFound
    [ApiVersion("2")]
    public async Task<IActionResult> DeleteRequest(Guid id)
    {
        var deletedShareRequest = await _db.ShareRequests
            .Where(x => x.Id == id && x.Owner == CurrentUser.Id).ExecuteDeleteAsync();
        
        if (deletedShareRequest <= 0) return Problem(ShareError.ShareRequestNotFound);
        
        return Ok();
    }
    
    [HttpDelete("requests/incoming/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShareRequestNotFound
    [ApiVersion("2")]
    public async Task<IActionResult> DenyRequest(Guid id)
    {
        var deletedShareRequest = await _db.ShareRequests
            .Where(x => x.Id == id && x.User == CurrentUser.Id).ExecuteDeleteAsync();
        
        if (deletedShareRequest <= 0) return Problem(ShareError.ShareRequestNotFound);
        
        return Ok();
    }

    // [HttpPost("requests/incoming/{id:guid}")]
    // [ProducesResponseType(StatusCodes.Status200OK)]
    // [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShareRequestNotFound
    // [ApiVersion("2")]
    // public async Task<IActionResult> RedeemRequest(Guid id)
    // {
    //     var shareRequest = await _db.ShareRequests
    //         .Where(x => x.Id == id && (x.User == null || x.User == CurrentUser.Id)).Include(x => x.ShareRequestsShockers).FirstOrDefaultAsync();
    //     
    //     if (shareRequest == null) return Problem(ShareError.ShareRequestNotFound);
    //     
    //     var alreadySharedShockers = await _db.ShockerShares.Where(x => x.Shocker.DeviceNavigation.OwnerNavigation.Id == shareRequest.Owner && x.SharedWith == CurrentUser.Id).Select(x => x.ShockerId).ToArrayAsync();
    //     
    //     foreach (var shareRequestShareRequestsShocker in shareRequest.ShareRequestsShockers)
    //     {
    //         
    //     }
    //     
    //     return Ok();
    // }
}

public class ShareRequestBase
{
    public required Guid Id { get; set; }
    public required DateTime CreatedOn { get; set; }
    public required GenericIni Owner { get; set; }
    public required GenericIni? SharedWith { get; set; }
}

public sealed class ShareRequestBaseItem : ShareRequestBase
{
    public required ShareRequestCounts Counts { get; set; }
    
    public sealed class ShareRequestCounts
    {
        public required int Shockers { get; set; }
    }
}

public sealed class ShareRequestBaseDetails : ShareRequestBase
{
    public required ShockerPermLimitPairWithId[] Shockers { get; set; }
}