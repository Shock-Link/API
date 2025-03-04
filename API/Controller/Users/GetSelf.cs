﻿using Microsoft.AspNetCore.Mvc;
using OpenShock.Common.Extensions;
using OpenShock.Common.Models;
using System.Net.Mime;

namespace OpenShock.API.Controller.Users;

public sealed partial class UsersController
{
    /// <summary>
    /// Get the current user's information.
    /// </summary>
    /// <response code="200">The user's information was successfully retrieved.</response>
    [HttpGet("self")]
    [ProducesResponseType<BaseResponse<SelfResponse>>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    public BaseResponse<SelfResponse> GetSelf()
    {
        return new BaseResponse<SelfResponse>
        {
            Data = new SelfResponse
            {
                Id = CurrentUser.Id,
                Name = CurrentUser.Name,
                Email = CurrentUser.Email,
                Image = CurrentUser.GetImageUrl(),
                Roles = CurrentUser.Roles,
                Rank = CurrentUser.Roles.Count > 0 ? CurrentUser.Roles.Max().ToString() : "User"
            }
        };
    }

    public sealed class SelfResponse
    {
        public required Guid Id { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required Uri Image { get; set; }
        public required List<RoleType> Roles { get; set; }
        public required string Rank { get; set; }
    }
}