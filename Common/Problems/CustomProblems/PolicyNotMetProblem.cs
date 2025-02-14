using System.Net;
using Microsoft.AspNetCore.Authorization;

namespace OpenShock.Common.Problems.CustomProblems;

public class PolicyNotMetProblem : OpenShockProblem
{
    public PolicyNotMetProblem(string[] failedRequirements) : base(
        "Authorization.Policy.NotMet",
        "One or multiple policies were not met", HttpStatusCode.Forbidden, string.Empty)
    {
        FailedRequirements = failedRequirements;
    }
    
    public string[] FailedRequirements { get; set; }
}