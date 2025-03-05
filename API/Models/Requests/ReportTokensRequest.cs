using System.ComponentModel.DataAnnotations;

namespace OpenShock.API.Models.Requests;

public class ReportTokensRequest
{
    [Required(AllowEmptyStrings = false)]
    public required string TurnstileResponse { get; set; }
    public required IEnumerable<string> Secrets { get; set; }
}