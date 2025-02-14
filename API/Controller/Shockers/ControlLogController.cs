using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Response;
using OpenShock.Common.Errors;
using OpenShock.Common.Extensions;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;
using OpenShock.Common.Utils;

namespace OpenShock.API.Controller.Shockers;

public sealed partial class ShockerController
{
    /// <summary>
    /// Get the logs for a shocker
    /// </summary>
    /// <param name="shockerId"></param>
    /// <param name="offset"></param>
    /// <param name="limit"></param>
    /// <response code="200">The logs</response>
    /// <response code="404">Shocker does not exist</response>
    [HttpGet("{shockerId}/logs")]
    [ProducesResponseType<BaseResponse<IAsyncEnumerable<LogEntry>>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ShockerNotFound
    [MapToApiVersion("1")]
    public async Task<IActionResult> GetShockerLogs([FromRoute] Guid shockerId, [FromQuery] uint offset = 0,
        [FromQuery] [Range(1, 500)] uint limit = 100)
    {
        var exists = await _db.Shockers.AnyAsync(x => x.DeviceNavigation.Owner == CurrentUser.Id && x.Id == shockerId);
        if (!exists) return Problem(ShockerError.ShockerNotFound);

        var logs = _db.ShockerControlLogs
            .Where(x => x.ShockerId == shockerId)
            .OrderByDescending(x => x.CreatedOn)
            .Skip((int)offset)
            .Take((int)limit)
            .Select(x => new LogEntry
            {
                Id = x.Id,
                Duration = x.Duration,
                Intensity = x.Intensity,
                Type = x.Type,
                CreatedOn = x.CreatedOn,
                ControlledBy = x.ControlledByNavigation == null
                    ? new ControlLogSenderLight
                    {
                        Id = Guid.Empty,
                        Name = "Guest",
                        Image = GravatarUtils.GuestImageUrl,
                        CustomName = x.CustomName
                    }
                    : new ControlLogSenderLight
                    {
                        Id = x.ControlledByNavigation.Id,
                        Name = x.ControlledByNavigation.Name,
                        Image = x.ControlledByNavigation.GetImageUrl(),
                        CustomName = x.CustomName
                    }
            })
            .AsAsyncEnumerable();

        return RespondSuccessLegacy(logs);
    }
}