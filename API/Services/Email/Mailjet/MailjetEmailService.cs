using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using OpenShock.API.Services.Email.Mailjet.Mail;

namespace OpenShock.API.Services.Email.Mailjet;

public sealed class MailjetEmailService : IEmailService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MailjetEmailService> _logger;
    private readonly ApiConfig.MailConfig.MailjetConfig _mailjetConfig;
    private readonly Contact _sender;

    /// <summary>
    /// DI Constructor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="mailjetConfig"></param>
    /// <param name="sender"></param>
    public MailjetEmailService(
        ILogger<MailjetEmailService> logger,
        ApiConfig.MailConfig.MailjetConfig mailjetConfig,
        Contact sender)
    {
        _logger = logger;
        _mailjetConfig = mailjetConfig;
        _sender = sender;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.mailjet.com/v3.1/"),
            DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{mailjetConfig.Key}:{mailjetConfig.Secret}"))) }
        };
    }

    #region Interface methods

    /// <inheritdoc />
    public async Task PasswordReset(Contact to, Uri resetLink, CancellationToken cancellationToken = default)
    {
        await SendMail(new TemplateMail
        {
            From = _sender,
            Subject = "Password reset request",
            To = [to],
            TemplateId = _mailjetConfig.Template.PasswordReset,
            Variables = new Dictionary<string, string>
            {
                {"link", resetLink.ToString() },
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task VerifyEmail(Contact to, Uri activationLink, CancellationToken cancellationToken = default)
    {
        await SendMail(new TemplateMail
        {
            From = _sender,
            Subject = "Verify your Email Address",
            To = [to],
            TemplateId = _mailjetConfig.Template.VerifyEmail,
            Variables = new Dictionary<string, string>
            {
                {"link", activationLink.ToString() },
            }
        }, cancellationToken);
    }

    #endregion
    
    private Task SendMail(MailBase templateMail, CancellationToken cancellationToken = default) => SendMails([templateMail], cancellationToken);

    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    
    private async Task SendMails(MailBase[] mails, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("Sending mails {@Mails}", mails);

        var json = JsonSerializer.Serialize(new MailsWrap
        {
            Messages = mails
        }, Options);
        
        var response = await _httpClient.PostAsync("send",
            new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error sending mails. Got unsuccessful status code {StatusCode} for mails {@Mails} with error body {Body}",
                response.StatusCode, mails, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        else _logger.LogDebug("Successfully sent mail");
    }
    
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}