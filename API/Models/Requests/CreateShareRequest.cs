using System.ComponentModel.DataAnnotations;
using OpenShock.Common.Constants;

namespace OpenShock.API.Models.Requests;

public sealed class CreateShareRequest
{
    [MaxLength(HardLimits.CreateShareRequestMaxShockers)]
    public required ShockerPermLimitPairWithId[] Shockers { get; set; }
    public Guid? User { get; set; } = null;
}

