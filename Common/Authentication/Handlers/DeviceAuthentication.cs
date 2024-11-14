using System.Net.Mime;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenShock.Common.Authentication.Services;
using OpenShock.Common.Errors;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Problems;
using OpenShock.Common.Utils;

namespace OpenShock.Common.Authentication.Handlers;

/// <summary>
/// Device / Box / The Thing / ESP32 authentication with DeviceToken header
/// </summary>
public sealed class DeviceAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IClientAuthService<Device> _authService;
    private readonly OpenShockContext _db;
    
    private readonly JsonSerializerOptions _serializerOptions;
    private OpenShockProblem? _authResultError = null;


    public DeviceAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IClientAuthService<Device> clientAuth,
        OpenShockContext db,
        IOptions<JsonOptions> jsonOptions)
        : base(options, logger, encoder)
    {
        _authService = clientAuth;
        _db = db;
        _serializerOptions = jsonOptions.Value.SerializerOptions;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Context.TryGetDeviceTokenFromHeader(out string? sessionKey))
        {
            return Fail(AuthResultError.CookieOrHeaderMissingOrInvalid);
        }

        var device = await _db.Devices.Where(x => x.Token == sessionKey).FirstOrDefaultAsync();
        if (device == null) return Fail(AuthResultError.TokenInvalid);

        _authService.CurrentClient = device;
        Context.Items["Device"] = _authService.CurrentClient.Id;

        var claims = new[]
        {
            new Claim("id", _authService.CurrentClient.Id.ToString()),
        };
        var ident = new ClaimsIdentity(claims, nameof(LoginSessionAuthentication));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(ident), Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private AuthenticateResult Fail(OpenShockProblem reason)
    {
        _authResultError = reason;
        return AuthenticateResult.Fail(reason.Type!);
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        _authResultError ??= AuthResultError.UnknownError;
        Response.StatusCode = _authResultError.Status!.Value;
        _authResultError.AddContext(Context);
        return Context.Response.WriteAsJsonAsync(_authResultError, _serializerOptions, contentType: MediaTypeNames.Application.ProblemJson);
    }
}