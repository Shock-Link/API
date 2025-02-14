namespace OpenShock.API.Models.Response;

public sealed class ResponseDeviceWithShockers : ResponseDevice
{
    public required ShockerResponse[] Shockers { get; set; }
}