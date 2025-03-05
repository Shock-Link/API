using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.API.Models.Requests;
using OpenShock.API.Models.Response;
using OpenShock.Common;
using OpenShock.Common.Authentication;
using OpenShock.Common.Constants;
using OpenShock.Common.Errors;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Problems;
using OpenShock.Common.Utils;

namespace OpenShock.API.Controller.Tokens;

public sealed partial class TokensController
{
    /// <summary>
    /// List all tokens for the current user
    /// </summary>
    /// <response code="200">All tokens for the current user</response>
    [HttpGet]
    [ProducesResponseType<IEnumerable<TokenResponse>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<IEnumerable<TokenResponse>> ListTokens()
    {
        var apiTokens = await _db.ApiTokens
            .Where(x => x.UserId == CurrentUser.Id && (x.ValidUntil == null || x.ValidUntil > DateTime.UtcNow))
            .OrderBy(x => x.CreatedOn)
            .Select(x => new TokenResponse
        {
            CreatedOn = x.CreatedOn,
            ValidUntil = x.ValidUntil,
            LastUsed = x.LastUsed,
            Permissions = x.Permissions,
            Name = x.Name,
            Id = x.Id
        }).ToListAsync();

        return apiTokens;
    }

    /// <summary>
    /// Get a token by id
    /// </summary>
    /// <param name="tokenId"></param>
    /// <response code="200">The token</response>
    /// <response code="404">The token does not exist or you do not have access to it.</response>
    [HttpGet("{tokenId}")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ApiTokenNotFound    
    public async Task<IActionResult> GetTokenById([FromRoute] Guid tokenId)
    {
        var apiToken = await _db.ApiTokens
            .Where(x => x.UserId == CurrentUser.Id && x.Id == tokenId && (x.ValidUntil == null || x.ValidUntil > DateTime.UtcNow))
            .Select(x => new TokenResponse
        {
            CreatedOn = x.CreatedOn,
            ValidUntil = x.ValidUntil,
            Permissions = x.Permissions,
            LastUsed = x.LastUsed,
            Name = x.Name,
            Id = x.Id
        }).FirstOrDefaultAsync();
        
        if (apiToken == null) return Problem(ApiTokenError.ApiTokenNotFound);
        
        return Ok(apiToken);
    }

    /// <summary>
    /// Revoke a token from the current user
    /// </summary>
    /// <param name="tokenId"></param>
    /// <response code="200">Successfully deleted token</response>
    /// <response code="404">The token does not exist or you do not have access to it.</response>
    [HttpDelete("{tokenId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ApiTokenNotFound    
    public async Task<IActionResult> DeleteToken([FromRoute] Guid tokenId)
    {
        var apiToken = await _db.ApiTokens
            .Where(x => x.Id == tokenId)
            .WhereIsUserOrPrivileged(x => x.User, CurrentUser)
            .ExecuteDeleteAsync();
        
        if (apiToken <= 0)
        {
            return Problem(ApiTokenError.ApiTokenNotFound);
        }
        
        return Ok();
    }

    /// <summary>
    /// Create a new token
    /// </summary>
    /// <param name="body"></param>
    /// <response code="200">The created token</response>
    [HttpPost]
    [ProducesResponseType<TokenCreatedResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<TokenCreatedResponse> CreateToken([FromBody] CreateTokenRequest body)
    {
        string token = CryptoUtils.RandomString(HardLimits.ApiKeyTokenLength);

        var tokenDto = new ApiToken
        {
            UserId = CurrentUser.Id,
            TokenHash = HashingUtils.HashSha256(token),
            CreatedByIp = HttpContext.GetRemoteIP(),
            Permissions = body.Permissions.Distinct().ToList(),
            Id = Guid.CreateVersion7(),
            Name = body.Name,
            ValidUntil = body.ValidUntil?.ToUniversalTime()
        };
        _db.ApiTokens.Add(tokenDto);
        await _db.SaveChangesAsync();

        return new TokenCreatedResponse
        {
            Token = token,
            Id = tokenDto.Id
        };
    }

    /// <summary>
    /// Edit a token
    /// </summary>
    /// <param name="tokenId"></param>
    /// <param name="body"></param>
    /// <response code="200">The edited token</response>
    /// <response code="404">The token does not exist or you do not have access to it.</response>
    [HttpPatch("{tokenId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OpenShockProblem>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)] // ApiTokenNotFound    
    public async Task<IActionResult> EditToken([FromRoute] Guid tokenId, [FromBody] EditTokenRequest body)
    {
        var token = await _db.ApiTokens
            .FirstOrDefaultAsync(x => x.UserId == CurrentUser.Id && x.Id == tokenId && (x.ValidUntil == null || x.ValidUntil > DateTime.UtcNow));
        if (token == null) return Problem(ApiTokenError.ApiTokenNotFound);

        token.Name = body.Name;
        token.Permissions = body.Permissions.Distinct().ToList();
        await _db.SaveChangesAsync();

        return Ok();
    }
}