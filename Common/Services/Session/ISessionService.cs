using OpenShock.Common.Redis;

namespace OpenShock.Common.Services.Session;

public interface ISessionService
{
    public Task<Guid> CreateSessionAsync(string sessionId, Guid userId, string userAgent, string ipAddress);

    public Task<LoginSession[]> ListSessionsByUserId(Guid userId);

    public Task<LoginSession?> GetSessionById(string sessionId);

    public Task<LoginSession?> GetSessionByPulbicId(Guid publicSessionId);

    public Task UpdateSession(LoginSession loginSession, TimeSpan ttl);

    public Task<bool> DeleteSessionById(string sessionId);

    public Task<bool> DeleteSessionByPublicId(Guid publicSessionId);

    public Task DeleteSession(LoginSession loginSession);
}