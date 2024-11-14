using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Asp.Versioning;
using OpenShock.Common;
using OpenShock.Common.Problems;
using OpenShock.Common.Utils;

namespace OpenShock.LiveControlGateway.Controllers;

/// <summary>
/// Controller for retrieving instance details.
/// </summary>
[ApiController]
[Route("/{version:apiVersion}")]
[ApiVersion("1")]
public sealed class InstanceDetailsController : OpenShockControllerBase
{
    private static readonly AssemblyName AssemblyName = typeof(InstanceDetailsController).Assembly.GetName();
    private static readonly string AssemblyVersion = AssemblyName.Version?.ToString() ?? "error";
    private static readonly string AssemblyNameString = AssemblyName.Name ?? "error";

    /// <summary>
    /// Retrieves information about the current LCG instance.
    /// </summary>
    /// <response code="200">Instance details was successfully retrieved.</response>
    [HttpGet]
    [ProducesResponseType<InstanceDetailsResponse>(StatusCodes.Status200OK, MediaTypeNames.Application.Json)]
    [MapToApiVersion("1")]
    public InstanceDetailsResponse GetNodeInfo([FromServices] LCGConfig lcgConfig)
    {
        return new InstanceDetailsResponse
        {
            Name = AssemblyNameString,
            Version = AssemblyVersion,
            Commit = GitHashAttribute.FullHash,
            CurrentTime = DateTimeOffset.UtcNow,
            Fqdn = lcgConfig.Lcg.Fqdn,
            CountryCode = lcgConfig.Lcg.CountryCode
        };
    }

    /// <summary>
    /// Details about an LCG instance.
    /// </summary>
    public sealed class InstanceDetailsResponse
    {
        /// <summary>
        /// Name of the instance.
        /// </summary>
        public required string Name { get; init; }
        
        /// <summary>
        /// Commit hash of the instance.
        /// </summary>
        public required string Commit { get; set; }

        /// <summary>
        /// Version of the instance.
        /// </summary>
        public required string Version { get; init; }

        /// <summary>
        /// Current time of the instance.
        /// </summary>
        public required DateTimeOffset CurrentTime { get; init; }

        /// <summary>
        /// Country code of the region the LCG instance is assigned to.
        /// </summary>
        public required string CountryCode { get; init; }

        /// <summary>
        /// Fully qualified domain name of the instance.
        /// </summary>
        public required string Fqdn { get; init; }
    }
}