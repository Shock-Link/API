using System.Net.Mime;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenShock.Common.Authentication.Services;
using OpenShock.Common.Constants;
using OpenShock.Common.Errors;
using OpenShock.Common.Models;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Problems;
using OpenShock.Common.Redis;
using OpenShock.Common.Services.BatchUpdate;
using OpenShock.Common.Services.Session;
using OpenShock.Common.Utils;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenShock.Common.Authentication.Handlers;

public sealed class LoginSessionAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IClientAuthService<LinkUser> _authService;
    private readonly IUserReferenceService _userReferenceService;
    private readonly IBatchUpdateService _batchUpdateService;
    private readonly OpenShockContext _db;
    private readonly ISessionService _sessionService;
    private readonly JsonSerializerOptions _serializerOptions;
    private OpenShockProblem? _authResultError = null;

    public LoginSessionAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IClientAuthService<LinkUser> clientAuth,
        IUserReferenceService userReferenceService,
        OpenShockContext db,
        ISessionService sessionService,
        IOptions<JsonOptions> jsonOptions, IBatchUpdateService batchUpdateService)
        : base(options, logger, encoder)
    {
        _authService = clientAuth;
        _userReferenceService = userReferenceService;
        _db = db;
        _sessionService = sessionService;
        _serializerOptions = jsonOptions.Value.SerializerOptions;
        _batchUpdateService = batchUpdateService;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.TryGetSessionKey(out var sessionKey))
        {
            return SessionAuth(sessionKey);
        }

        if (Context.TryGetAuthTokenFromHeader(out var token))
        {
            return TokenAuth(token);
        }

        return Task.FromResult(Fail(AuthResultError.CookieOrHeaderMissingOrInvalid));
    }

    private async Task<AuthenticateResult> TokenAuth(string token)
    {
        var tokenDto = await _db.ApiTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.Token == token &&
            (x.ValidUntil == null || x.ValidUntil >= DateTime.UtcNow));
        if (tokenDto == null) return Fail(AuthResultError.TokenInvalid);

        _batchUpdateService.UpdateTokenLastUsed(tokenDto.Id);
        _authService.CurrentClient = new LinkUser
        {
            DbUser = tokenDto.User
        };
        _userReferenceService.AuthReference = tokenDto;

        Context.Items["User"] = _authService.CurrentClient.DbUser.Id;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _authService.CurrentClient.DbUser.Id.ToString()),
            new(ControlLogAdditionalItem.ApiTokenId, tokenDto.Id.ToString())
        };

        var ident = new ClaimsIdentity(claims, nameof(LoginSessionAuthentication));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(ident), Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private async Task<AuthenticateResult> SessionAuth(string sessionKey)
    {
        var session = await _sessionService.GetSessionById(sessionKey);
        if (session == null) return Fail(AuthResultError.SessionInvalid);

        if (session.Expires!.Value < DateTime.UtcNow.Subtract(Duration.LoginSessionExpansionAfter))
        {
#pragma warning disable CS4014
            LucTask.Run(async () =>
#pragma warning restore CS4014
            {
                session.Expires = DateTime.UtcNow.Add(Duration.LoginSessionLifetime);
                await _sessionService.UpdateSession(session, Duration.LoginSessionLifetime);
            });
        }
        
        _batchUpdateService.UpdateSessionLastUsed(sessionKey, DateTime.UtcNow);
        
        var retrievedUser = await _db.Users.FirstAsync(user => user.Id == session.UserId);

        _userReferenceService.AuthReference = session;
        _authService.CurrentClient = new LinkUser
        {
            DbUser = retrievedUser
        };

        Context.Items["User"] = _authService.CurrentClient.DbUser.Id;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _authService.CurrentClient.DbUser.Id.ToString())
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