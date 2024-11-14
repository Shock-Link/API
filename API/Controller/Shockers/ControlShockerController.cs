using System.Net;
using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OpenShock.Common.Authentication.Attributes;
using OpenShock.Common.DeviceControl;
using OpenShock.Common.Errors;
using OpenShock.Common.Hubs;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;
using OpenShock.Common.Services.RedisPubSub;
using OpenShock.Common.Utils;

namespace OpenShock.API.Controller.Shockers;

public sealed partial class ShockerController
{
    private static readonly IDictionary<string, object> EmptyDic = new Dictionary<string, object>();

    /// <summary>
    /// Send a control message to shockers
    /// </summary>
    /// <response code="200">The control messages were successfully sent.</response>
    [MapToApiVersion("2")]
    [HttpPost("control")]
    [TokenPermission(PermissionType.Shockers_Use)]
    [ProducesResponseType<BaseResponse<object>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // Shocker not found
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status412PreconditionFailed, MediaTypeNames.Application.ProblemJson)] // Shocker is paused
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)] // You don't have permission to control this shocker
    public async Task<IActionResult> SendControl(
        [FromBody] ControlRequest body,
        [FromServices] IHubContext<UserHub, IUserHub> userHub,
        [FromServices] IRedisPubService redisPubService)
    {
        var sender = new ControlLogSender
        {
            Id = CurrentUser.DbUser.Id,
            Name = CurrentUser.DbUser.Name,
            Image = GravatarUtils.GetImageUrl(CurrentUser.DbUser.Email),
            ConnectionId = HttpContext.Connection.Id,
            AdditionalItems = EmptyDic,
            CustomName = body.CustomName
        };

        var controlAction = await ControlLogic.ControlByUser(body.Shocks, _db, sender, userHub.Clients, redisPubService);
        return controlAction.Match(
            success => RespondSuccessLegacySimple("Successfully sent control messages"),
            notFound => Problem(ShockerControlError.ShockerControlNotFound(notFound.Value)),
            paused => Problem(ShockerControlError.ShockerControlPaused(paused.Value)),
            noPermission => Problem(ShockerControlError.ShockerControlNoPermission(noPermission.Value)));
    }

    /// <summary>
    /// Send a control message to shockers (Deprecated in favor of the /2/shockers/control endpoint)
    /// </summary>
    /// <response code="200">The control messages were successfully sent.</response>
    [MapToApiVersion("1")]
    [HttpPost("control")]
    [TokenPermission(PermissionType.Shockers_Use)]
    [ProducesResponseType<BaseResponse<object>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // Shocker not found
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status412PreconditionFailed, MediaTypeNames.Application.ProblemJson)] // Shocker is paused
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)] // You don't have permission to control this shocker
    public Task<IActionResult> SendControl_DEPRECATED(
        [FromBody] IEnumerable<Common.Models.WebSocket.User.Control> body,
        [FromServices] IHubContext<UserHub, IUserHub> userHub,
        [FromServices] IRedisPubService redisPubService)
    {
        return SendControl(new ControlRequest
        {
            Shocks = body,
            CustomName = null
        }, userHub, redisPubService);
    }
}