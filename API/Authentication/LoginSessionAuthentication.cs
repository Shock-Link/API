﻿using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Redis.OM.Contracts;
using Redis.OM.Searching;
using ShockLink.API.Models;
using ShockLink.Common.Redis;
using ShockLink.Common.ShockLinkDb;

namespace ShockLink.API.Authentication;

public class LoginSessionAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
}

public class LoginSessionAuthentication : AuthenticationHandler<LoginSessionAuthenticationSchemeOptions>
{
    private readonly IClientAuthService<LinkUser> _authService;
    private readonly IMemoryCache _memoryCache;
    private readonly ShockLinkContext _db;
    private readonly IRedisCollection<LoginSession> _userSessions;
    private string _failReason = "Internal server error";

    public LoginSessionAuthentication(IOptionsMonitor<LoginSessionAuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IClientAuthService<LinkUser> clientAuth,
        IMemoryCache memoryCache, ShockLinkContext db, IRedisConnectionProvider provider)
        : base(options, logger, encoder, clock)
    {
        _authService = clientAuth;
        _memoryCache = memoryCache;
        _db = db;
        _userSessions = provider.RedisCollection<LoginSession>(false);
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string sessionKey;

        if (Context.Request.Headers.TryGetValue("ShockLinkSession", out var sessionKeyHeader) &&
            !string.IsNullOrEmpty(sessionKeyHeader))
        {
            sessionKey = sessionKeyHeader!;
        }
        else if (Context.Request.Cookies.TryGetValue("shockLinkSession", out var accessKeyCookie) &&
                 !string.IsNullOrEmpty(accessKeyCookie))
        {
            sessionKey = accessKeyCookie;
        }
        else return Fail("ShockLinkSession header/cookie was not found");

        var session = await _userSessions.FindByIdAsync(sessionKey);
        if(session == null) return Fail("AbiSession was not found");

        var retrievedUser = await _db.Users.FirstAsync(user => user.Id == session.UserId );

        _authService.CurrentClient = new LinkUser
        {
            DbUser = retrievedUser
        };

        Context.Items["User"] = _authService.CurrentClient.DbUser.Id;

        var claims = new[]
        {
            new Claim("id", _authService.CurrentClient.DbUser.Id.ToString()),
        };
        var ident = new ClaimsIdentity(claims, nameof(LoginSessionAuthentication));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(ident), Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private AuthenticateResult Fail(string reason)
    {
        _failReason = reason;
        return AuthenticateResult.Fail(reason);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        return Context.Response.WriteAsJsonAsync(new BaseResponse<object>
        {
            Message = _failReason
        });
    }
}