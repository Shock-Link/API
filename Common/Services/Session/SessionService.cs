using Microsoft.EntityFrameworkCore;
using OpenShock.Common.Authentication.AuthenticationHandlers;
using OpenShock.Common.Constants;
using OpenShock.Common.Redis;
using Redis.OM;
using Redis.OM.Contracts;
using Redis.OM.Searching;

namespace OpenShock.Common.Services.Session;

/// <summary>
/// Default implementation of ISessionService
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly IRedisCollection<LoginSession> _loginSessions;

    /// <summary>
    /// DI constructor
    /// </summary>
    /// <param name="redisConnectionProvider"></param>
    public SessionService(IRedisConnectionProvider redisConnectionProvider)
    {
        _loginSessions = redisConnectionProvider.RedisCollection<LoginSession>(false);
    }

    public async Task<Guid> CreateSessionAsync(string sessionId, Guid userId, string userAgent, string ipAddress)
    {
        Guid publicId = Guid.CreateVersion7();

        await _loginSessions.InsertAsync(new LoginSession
        {
            Id = sessionId,
            UserId = userId,
            UserAgent = userAgent,
            Ip = ipAddress,
            PublicId = publicId,
            Created = DateTime.UtcNow,
            Expires = DateTime.UtcNow.Add(Duration.LoginSessionLifetime),
        }, Duration.LoginSessionLifetime);

        return publicId;
    }

    public async Task<LoginSession[]> ListSessionsByUserId(Guid userId)
    {
        var sessions = await _loginSessions.Where(x => x.UserId == userId).ToArrayAsync();

        var needsSave = false;
        foreach (var session in sessions)
        {
            needsSave |= UpdateOlderSessions(session);
        }

        if (needsSave)
        {
            await _loginSessions.SaveAsync();
        }

        return sessions;
    }

    public async Task<LoginSession?> GetSessionById(string sessionId)
    {
        var session = await _loginSessions.FindByIdAsync(sessionId);

        if (UpdateOlderSessions(session)) await _loginSessions.SaveAsync();

        return session;
    }

    public async Task<LoginSession?> GetSessionByPulbicId(Guid publicSessionId)
    {
        var session = await _loginSessions.Where(x => x.PublicId == publicSessionId).FirstOrDefaultAsync();

        if (UpdateOlderSessions(session)) await _loginSessions.SaveAsync();

        return session;
    }

    public async Task UpdateSession(LoginSession session, TimeSpan ttl)
    {
        await _loginSessions.UpdateAsync(session, ttl);
    }

    public async Task<bool> DeleteSessionById(string sessionId)
    {
        var session = await _loginSessions.FindByIdAsync(sessionId);
        if (session == null) return false;

        await _loginSessions.DeleteAsync(session);
        return true;
    }

    public async Task<bool> DeleteSessionByPublicId(Guid publicSessionId)
    {
        var session = await _loginSessions.Where(x => x.PublicId == publicSessionId).FirstOrDefaultAsync();
        if (session == null) return false;

        await _loginSessions.DeleteAsync(session);
        return true;
    }

    public async Task DeleteSession(LoginSession loginSession)
    {
        await _loginSessions.DeleteAsync(loginSession);
    }

    private static bool UpdateOlderSessions(LoginSession? session)
    {
        if (session == null) return false;

        var save = false;

        if (session.PublicId == null)
        {
            session.PublicId = Guid.CreateVersion7();
            save = true;
        }

        if (session.Created == null)
        {
            session.Created = DateTime.UtcNow;
            save = true;
        }

        if (session.Expires == null)
        {
            session.Expires = DateTime.UtcNow;
            save = true;
        }

        return save;
    }
}