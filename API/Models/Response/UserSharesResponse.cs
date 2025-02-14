using OpenShock.Common.Models;
using OpenShock.Common.Models.WebSocket.User;

namespace OpenShock.API.Models.Response;

public sealed class UserSharesResponse
{
    public required UserShareInfo[] Shockers { get; init; }
}

public sealed class UserShareInfo : GenericIn
{
    public required DateTime CreatedOn { get; set; }
    public required ShockerPermissions Permissions { get; set; }
    public required ShockerLimits Limits { get; set; }
    public required bool Paused { get; set; }
}