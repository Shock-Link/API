using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using OneOf;
using OneOf.Types;
using OpenShock.Common.Extensions;
using OpenShock.Common.Models;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Redis.PubSub;
using OpenShock.Common.Services.RedisPubSub;
using OpenShock.LiveControlGateway.Controllers;
using OpenShock.LiveControlGateway.Websocket;
using Redis.OM.Contracts;
using Semver;

namespace OpenShock.LiveControlGateway.LifetimeManager;

/// <summary>
/// Lifetime manager for devices, this class is responsible for managing the lifetime of devices
/// </summary>
public sealed class HubLifetimeManager
{
    private readonly IDbContextFactory<OpenShockContext> _dbContextFactory;
    private readonly IRedisConnectionProvider _redisConnectionProvider;
    private readonly IRedisPubService _redisPubService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HubLifetimeManager> _logger;
    
    private readonly Dictionary<Guid, HubLifetime> _lifetimes = new();
    private readonly SemaphoreSlim _lifetimesLock = new(1);

    /// <summary>
    /// DI constructor
    /// </summary>
    /// <param name="dbContextFactory"></param>
    /// <param name="redisConnectionProvider"></param>
    /// <param name="redisPubService"></param>
    /// <param name="loggerFactory"></param>
    public HubLifetimeManager(
        IDbContextFactory<OpenShockContext> dbContextFactory,
        IRedisConnectionProvider redisConnectionProvider,
        IRedisPubService redisPubService,
        ILoggerFactory loggerFactory
    )
    {
        _dbContextFactory = dbContextFactory;
        _redisConnectionProvider = redisConnectionProvider;
        _redisPubService = redisPubService;
        _loggerFactory = loggerFactory;

        _logger = _loggerFactory.CreateLogger<HubLifetimeManager>();
    }

    /// <summary>
    /// Add device to lifetime manager, called on successful connect of device
    /// </summary>
    /// <param name="tps"></param>
    /// <param name="hubController"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<bool> TryAddDeviceConnection(byte tps, IHubController hubController,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Adding hub lifetime [{HubId}]", hubController.Id);
        var isSwapping = false;
        HubLifetime? hubLifetime;

        using (await _lifetimesLock.LockAsyncScoped(cancellationToken))
        {
            if (_lifetimes.TryGetValue(hubController.Id, out hubLifetime))
            {
                // There already is a hub lifetime, lets swap!
                if (!hubLifetime.TryMarkSwapping())
                {
                    return
                        false; // Tell the controller that we are busy right now TODO: Tell the connecting client why it failed
                }

                isSwapping = true;
            }
            else
            {
                // This is a fresh connection with no existing lifetime, create one!
                hubLifetime = CreateNewLifetime(tps, hubController);
                _lifetimes[hubController.Id] = hubLifetime;
            }
        }


        if (isSwapping)
        {
            _logger.LogTrace("Swapping hub lifetime [{HubId}]", hubController.Id);
            await hubLifetime.Swap(hubController);
        }
        else
        {
            _logger.LogTrace("Initializing hub lifetime [{HubId}]", hubController.Id);
            if (!await hubLifetime.InitAsync(cancellationToken))
            {
                // If we fail to initialize, the hub must be removed
                await RemoveDeviceConnection(hubController, false); // Here be dragons?
                return false;
            }

            foreach (var websocketController in WebsocketManager.LiveControlUsers.GetConnections(hubLifetime.HubController.Id))
                await websocketController.UpdateConnectedState(true);
        }

        return true;
    }

    private HubLifetime CreateNewLifetime(byte tps, IHubController hubController)
    {
        _logger.LogInformation("New hub connected, creating lifetime [{DeviceId}]", hubController.Id);

        var deviceLifetime = new HubLifetime(
            tps,
            hubController,
            _dbContextFactory,
            _redisConnectionProvider,
            _redisPubService,
            _loggerFactory.CreateLogger<HubLifetime>());

        return deviceLifetime;
    }

    /// <summary>
    /// Remove device from Lifetime Manager, called on dispose of device controller,
    /// this is the actual end of life of the hub
    /// </summary>
    /// <param name="hubController"></param>
    /// <param name="notifyLiveControlClients"></param>
    public async Task RemoveDeviceConnection(IHubController hubController, bool notifyLiveControlClients = true)
    {
        _logger.LogDebug("Removing hub lifetime [{HubId}]", hubController.Id);
        HubLifetime? hubLifetime;
        
        using (await _lifetimesLock.LockAsyncScoped())
        {
            if (!_lifetimes.TryGetValue(hubController.Id, out hubLifetime))
            {
                _logger.LogError("Hub lifetime not found for hub [{HubId}]", hubController.Id);
                return;
            }
            
            // Dont remove a hub lifetime that has a different hub controller,
            // this might happen when remove is called after a swap has been fully done
            if(hubLifetime.HubController != hubController) return;

            if (!hubLifetime.TryMarkRemoving())
            {
                return;
            }
        }

        if (notifyLiveControlClients)
        {
            foreach (var websocketController in WebsocketManager.LiveControlUsers.GetConnections(hubController.Id))
                await websocketController.UpdateConnectedState(false);
        }
        
        await hubLifetime.DisposeAsync();
        
        using (await _lifetimesLock.LockAsyncScoped())
        {
            if (!_lifetimes.Remove(hubController.Id))
            {
                _logger.LogError("Failed to remove hub lifetime [{HubId}], this shouldnt happen WTF?!", hubController.Id);
            }
        }
    }

    /// <summary>
    /// Check if device is connected to LCG
    /// </summary>
    /// <param name="device"></param>
    /// <returns></returns>
    public bool IsConnected(Guid device) => _lifetimes.ContainsKey(device);

    /// <summary>
    /// Receive a control frame by a client, this implies that limits and permissions have been checked before
    /// </summary>
    /// <param name="device"></param>
    /// <param name="shocker"></param>
    /// <param name="type"></param>
    /// <param name="intensity"></param>
    /// <param name="tps"></param>
    /// <returns></returns>
    public OneOf<Success, DeviceNotFound, ShockerNotFound, ShockerExclusive> ReceiveFrame(Guid device, Guid shocker,
        ControlType type, byte intensity, byte tps)
    {
        if (!_lifetimes.TryGetValue(device, out var deviceLifetime)) return new DeviceNotFound();
        var receiveFrameAction = deviceLifetime.ReceiveFrame(shocker, type, intensity, tps);
        if (receiveFrameAction.IsT0) return new Success();
        if (receiveFrameAction.IsT1) return new ShockerNotFound();
        return receiveFrameAction.AsT2;
    }

    /// <summary>
    /// Update device data from the database
    /// </summary>
    /// <param name="device"></param>
    /// <returns></returns>
    public async Task<OneOf<Success, DeviceNotFound>> UpdateDevice(Guid device)
    {
        if (!_lifetimes.TryGetValue(device, out var deviceLifetime)) return new DeviceNotFound();
        await deviceLifetime.UpdateDevice();
        return new Success();
    }

    /// <summary>
    /// Control from redis, aka a regular command
    /// </summary>
    /// <param name="device"></param>
    /// <param name="shocks"></param>
    /// <returns></returns>
    public async Task<OneOf<Success, DeviceNotFound>> Control(Guid device,
        IList<ControlMessage.ShockerControlInfo> shocks)
    {
        if (!_lifetimes.TryGetValue(device, out var deviceLifetime)) return new DeviceNotFound();
        await deviceLifetime.Control(shocks);
        return new Success();
    }

    /// <summary>
    /// Captive portal control from redis
    /// </summary>
    /// <param name="device"></param>
    /// <param name="enabled"></param>
    /// <returns></returns>
    public async Task<OneOf<Success, DeviceNotFound>> ControlCaptive(Guid device, bool enabled)
    {
        if (!_lifetimes.TryGetValue(device, out var deviceLifetime)) return new DeviceNotFound();
        await deviceLifetime.ControlCaptive(enabled);
        return new Success();
    }

    /// <summary>
    /// Ota start install
    /// </summary>
    /// <param name="device"></param>
    /// <param name="version"></param>
    /// <returns></returns>
    public async Task<OneOf<Success, DeviceNotFound>> OtaInstall(Guid device, SemVersion version)
    {
        if (!_lifetimes.TryGetValue(device, out var deviceLifetime)) return new DeviceNotFound();
        await deviceLifetime.OtaInstall(version);
        return new Success();
    }

    /// <summary>
    /// Set device online, or update its status
    /// </summary>
    /// <param name="device"></param>
    /// <param name="data"></param>
    public async Task<OneOf<Success, DeviceNotFound>> DeviceOnline(Guid device, SelfOnlineData data)
    {
        if (!_lifetimes.TryGetValue(device, out var deviceLifetime)) return new DeviceNotFound();
        await deviceLifetime.Online(device, data);
        return new Success();
    }
}

/// <summary>
/// OneOf
/// </summary>
public readonly struct DeviceNotFound;

/// <summary>
/// OneOf
/// </summary>
public readonly struct ShockerNotFound;

/// <summary>
/// OneOf
/// </summary>
public readonly record struct ShockerExclusive(DateTimeOffset Until);