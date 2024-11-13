using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.Common.Redis;
using Redis.OM;
using System.Net;
using OpenShock.Common.Errors;
using OpenShock.Common.Problems;
using OpenShock.Common.Models;

namespace OpenShock.API.Controller.Device;

public sealed partial class DeviceController
{
    /// <summary>
    /// Pair a device with a pair code.
    /// </summary>
    /// <param name="pairCode">The pair code to pair with.</param>
    /// <response code="200">Successfully assigned LCG node</response>
    /// <response code="404">No such pair code exists</response>
    [AllowAnonymous]
    [HttpGet("pair/{pairCode}", Name = "Pair")]
    [HttpGet("~/{version:apiVersion}/pair/{pairCode}", Name = "Pair_DEPRECATED")] // Backwards compatibility
    [ProducesResponseType<BaseResponse<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, "application/problem+json")] // PairCodeNotFound
    public async Task<IActionResult> Pair([FromRoute] string pairCode)
    {
        var devicePairs = _redis.RedisCollection<DevicePair>();

        var pair = await devicePairs.Where(x => x.PairCode == pairCode).FirstOrDefaultAsync();
        if (pair == null) return Problem(PairError.PairCodeNotFound);
        await devicePairs.DeleteAsync(pair);

        var device = await _db.Devices.FirstOrDefaultAsync(x => x.Id == pair.Id);
        if (device == null) throw new Exception("Device not found for pair code");

        return RespondSuccessLegacy(device.Token);
    }
}