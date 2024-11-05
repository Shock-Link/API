﻿using System.ComponentModel.DataAnnotations;
using OpenShock.API.Models.Response;
using OpenShock.Common;
using OpenShock.Common.Constants;

namespace OpenShock.API.Models.Requests;

public sealed class ShareLinkEditShocker
{
    public required ShockerPermissions Permissions { get; set; }
    public required ShockerLimits Limits { get; set; }

    [Range(HardLimits.MinControlDuration, HardLimits.MaxControlDuration)]
    public ushort? Cooldown { get; set; }
}