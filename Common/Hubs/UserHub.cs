using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenShock.Common.Authentication;
using OpenShock.Common.Authentication.Services;
using OpenShock.Common.DeviceControl;
using OpenShock.Common.Extensions;
using OpenShock.Common.Models;
using OpenShock.Common.Models.WebSocket;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Redis;
using OpenShock.Common.Services.RedisPubSub;
using OpenShock.Common.Utils;
using Redis.OM;
using Redis.OM.Contracts;
using Semver;

namespace OpenShock.Common.Hubs;

[Authorize(AuthenticationSchemes = OpenShockAuthSchemas.UserSessionApiTokenCombo)]
public sealed class UserHub : Hub<IUserHub>
{
    private readonly ILogger<UserHub> _logger;
    private readonly OpenShockContext _db;
    private readonly IRedisConnectionProvider _provider;
    private readonly IRedisPubService _redisPubService;
    private readonly IUserReferenceService _userReferenceService;
    private IReadOnlyCollection<PermissionType>? _tokenPermissions = null;

    public UserHub(ILogger<UserHub> logger, OpenShockContext db, IRedisConnectionProvider provider,
        IRedisPubService redisPubService, IUserReferenceService userReferenceService)
    {
        _logger = logger;
        _db = db;
        _provider = provider;
        _redisPubService = redisPubService;
        _userReferenceService = userReferenceService;
    }

    public override async Task OnConnectedAsync()
    {
        _tokenPermissions = _userReferenceService.AuthReference is not { IsT1: true } ? null : _userReferenceService.AuthReference.Value.AsT1.Permissions;

        await Clients.Caller.Welcome(Context.ConnectionId);
        var devicesOnline = _provider.RedisCollection<DeviceOnline>(false);
        var sharedDevices = await _db.Devices
            .Where(x => x.Shockers.Any(y => y.ShockerShares.Any(z => z.SharedWith == UserId)))
            .Select(x => x.Id.ToString()).ToArrayAsync();

        var own = devicesOnline.Where(x => x.Owner == UserId).ToArrayAsync();
        var shared = devicesOnline.FindByIdsAsync(sharedDevices);
        await Task.WhenAll(own, shared);

        var final = new List<DeviceOnlineState>();
        final.AddRange(own.Result.Select(x =>
            new DeviceOnlineState
            {
                Device = x.Id,
                Online = true,
                FirmwareVersion = x.FirmwareVersion
            }));
        final.AddRange(shared.Result.Values.Where(x => x != null).Select(x =>
            new DeviceOnlineState
            {
                Device = x!.Id,
                Online = true,
                FirmwareVersion = x.FirmwareVersion
            }));
        await Clients.Caller.DeviceStatus(final);
    }

    public Task Control(Models.WebSocket.User.Control[] shocks)
    {
        return ControlV2(shocks, null);
    }

    public async Task ControlV2(Models.WebSocket.User.Control[] shocks, string? customName)
    {
        if (!_tokenPermissions.IsAllowedAllowOnNull(PermissionType.Shockers_Use)) return;

        var additionalItems = new Dictionary<string, object>();
        var apiTokenId = Context.User?.FindFirst(OpenShockAuthClaims.ApiTokenId);
        if (apiTokenId != null) additionalItems[OpenShockAuthClaims.ApiTokenId] = apiTokenId.Value;

        var sender = await _db.Users.Where(x => x.Id == UserId).Select(x => new ControlLogSender
        {
            Id = x.Id,
            Name = x.Name,
            Image = x.GetImageUrl(),
            ConnectionId = Context.ConnectionId,
            AdditionalItems = additionalItems,
            CustomName = customName
        }).SingleAsync();

        await ControlLogic.ControlByUser(shocks, _db, sender, Clients, _redisPubService);
    }

    public async Task CaptivePortal(Guid deviceId, bool enabled)
    {
        // Require a user session basically
        if (_tokenPermissions != null) return;

        var devices = await _db.Devices.Where(x => x.Owner == UserId)
            .AnyAsync(x => x.Id == deviceId);
        if (!devices) return;

        await _redisPubService.SendDeviceCaptivePortal(deviceId, enabled);
    }

    public async Task OtaInstall(Guid deviceId, SemVersion version)
    {
        // Require a user session basically
        if (_tokenPermissions != null) return;

        var devices = await _db.Devices.Where(x => x.Owner == UserId)
            .AnyAsync(x => x.Id == deviceId);
        if (!devices) return;

        await _redisPubService.SendDeviceOtaInstall(deviceId, version);
    }


    private Task<User> GetUser() => GetUser(UserId, _db);

    private Guid UserId => _userId ??= Guid.Parse(Context.UserIdentifier!);
    private Guid? _userId;
    private static Task<User> GetUser(Guid userId, OpenShockContext db) => db.Users.SingleAsync(x => x.Id == userId);
}