namespace OpenShock.API.Models.Requests;

public sealed class CreateTokenRequest : EditTokenRequest
{
    public DateTime? ValidUntil { get; set; } = null;
}