namespace OpenShock.API.Models.Response;

public sealed class TokenCreatedResponse
{
    public required string Token { get; set; }
    public required Guid Id { get; set; }
}