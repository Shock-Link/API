using System.Net;

namespace OpenShock.Common.OpenShockDb;

public class ApiTokenReport
{
    public required Guid Id { get; set; }

    public required DateTimeOffset ReportedAt { get; set; }

    public required Guid ReportedByUserId { get; set; }

    public required IPAddress ReportedByIp { get; set; }

    public string? ReportedByIpCountry { get; set; } = null;

    public virtual User ReportedByUser { get; set; } = null!;
}
