using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using NRedisStack.RedisStackCommands;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;
using OpenShock.Common.Redis;
using Redis.OM;
using Redis.OM.Contracts;
using StackExchange.Redis;

namespace OpenShock.API.Controller.Public;

public sealed partial class PublicController
{
    /// <summary>
    /// Gets online devices statistics
    /// </summary>
    /// <response code="200">The statistics were successfully retrieved.</response>
    [HttpGet("stats")]
    [ProducesResponseType<BaseResponse<StatsResponse>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetOnlineDevicesStatistics([FromServices] IConnectionMultiplexer redisConnectionMultiplexer)
    {
        var ft = redisConnectionMultiplexer.GetDatabase().FT();
        var deviceOnlineInfo = await ft.InfoAsync(DeviceOnline.IndexName);

        return RespondSuccessLegacy(new StatsResponse
        {
            DevicesOnline = deviceOnlineInfo.NumDocs
        });
    }
}

public sealed class StatsResponse
{
    public required long DevicesOnline { get; set; }
}