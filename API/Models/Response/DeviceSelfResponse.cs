namespace OpenShock.API.Models.Response;

public sealed class DeviceSelfResponse
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required MinimalShocker[] Shockers { get; set; }
}