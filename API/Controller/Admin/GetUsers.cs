using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenShock.Common.Errors;
using OpenShock.Common.Models;
using OpenShock.Common.Utils;
using OpenShock.Common.Problems;
using Z.EntityFramework.Plus;
using OpenShock.Common.OpenShockDb;

namespace OpenShock.API.Controller.Admin;

public sealed partial class AdminController
{
    /// <summary>
    /// Gets all users, paginated
    /// </summary>
    /// <response code="200">Paginated users</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet("users")]
    [ProducesResponseType<Paginated<AdminUsersView>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public async Task<IActionResult> GetUsers(
        [FromQuery(Name = "$filter")] string filterQuery = "",
        [FromQuery(Name = "$orderby")] string orderbyQuery = "",
        [FromQuery(Name = "$offset")] [Range(0, int.MaxValue)] int offset = 0,
        [FromQuery(Name = "$limit")] [Range(1, 1000)] int limit = 100
        )
    {
        var deferredCount = _db.Users.DeferredLongCount().FutureValue();

        var query = _db.AdminUsersViews.AsNoTracking();

        try
        {
            if (!string.IsNullOrEmpty(filterQuery))
            {
                query = query.ApplyFilter(filterQuery);
            }

            if (!string.IsNullOrEmpty(orderbyQuery))
            {
                query = query.ApplyOrderBy(orderbyQuery);
            }
            else
            {
                query = query.OrderBy(u => u.CreatedAt);
            }
        }
        catch (ExpressionBuilder.ExpressionException e)
        {
            return Problem(ExpressionError.ExpressionExceptionError(e.Message));
        }

        if (offset != 0)
        {
            query = query.Skip(offset);
        }
        
        var deferredUsers = query.Take(limit).Future();

        return Ok(new Paginated<AdminUsersView>
        {
            Data = await deferredUsers.ToListAsync(),
            Offset = offset,
            Limit = limit,
            Total = await deferredCount.ValueAsync(),
        });
    }
}