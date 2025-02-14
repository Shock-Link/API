using System.Text.Json.Serialization;
using OpenShock.API.Utils;

namespace OpenShock.API.Services.Email.Mailjet.Mail;

[JsonConverter(typeof(OneWayPolymorphicJsonConverter<MailBase>))]
public abstract class MailBase
{
    public required Contact From  { get; set; }
    public required Contact[] To { get; set; }
    public required string Subject { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
}