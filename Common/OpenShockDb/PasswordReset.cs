﻿using System;
using System.Collections.Generic;

namespace OpenShock.Common.OpenShockDb;

public partial class PasswordReset
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UsedOn { get; set; }

    public string Secret { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
