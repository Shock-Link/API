using OpenShock.Common.Models;
using OpenShock.Common.Models.WebSocket;
using OpenShock.Common.Models.WebSocket.User;
using OpenShock.Serialization.Types;
using Semver;

namespace OpenShock.Common.Hubs;

public interface IUserHub
{
    Task Welcome(string connectionId);
    Task DeviceStatus(IList<DeviceOnlineState> deviceOnlineStates);
    Task Log(ControlLogSender sender, IEnumerable<ControlLog> logs);
    Task DeviceUpdate(Guid deviceId, DeviceUpdateType type);
    
    // OTA
    Task OtaInstallStarted(Guid deviceId, int updateId, SemVersion version);
    Task OtaInstallProgress(Guid deviceId, int updateId, OtaUpdateProgressTask task, float progress);
    Task OtaInstallFailed(Guid deviceId, int updateId, bool fatal, string message);
    Task OtaRollback(Guid deviceId, int updateId);
    Task OtaInstallSucceeded(Guid deviceId, int updateId);
}