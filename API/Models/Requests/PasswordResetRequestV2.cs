using OpenShock.Common.DataAnnotations;

namespace OpenShock.API.Models.Requests;

public sealed class PasswordResetRequestV2
{
    [EmailAddress(true)]
    public required string Email { get; set; }
    
    [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = false)]
    public required string TurnstileResponse { get; set; }
}