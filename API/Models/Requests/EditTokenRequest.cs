using System.ComponentModel.DataAnnotations;
using OpenShock.Common.Constants;
using OpenShock.Common.Models;

namespace OpenShock.API.Models.Requests;

public class EditTokenRequest
{
    [StringLength(HardLimits.ApiKeyNameMaxLength, MinimumLength = 1, ErrorMessage = "API token length must be between {1} and {2}")]
    public required string Name { get; set; }
        
    [MaxLength(HardLimits.ApiKeyMaxPermissions, ErrorMessage = "API token permissions must be between {1} and {2}")]
    public List<PermissionType> Permissions { get; set; } = [PermissionType.Shockers_Use];
}