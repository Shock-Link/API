using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using OpenShock.Common.Models;
using OpenShock.Common.Problems;

namespace OpenShock.Common;

[Consumes(MediaTypeNames.Application.Json)]
public class OpenShockControllerBase : ControllerBase
{
    [NonAction]
    public ObjectResult Problem(OpenShockProblem problem) => problem.ToObjectResult(HttpContext);
    
    [NonAction]
    public ObjectResult RespondSuccessLegacy<T>(T data, string message = "", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        Response.StatusCode = (int)statusCode;
        return new ObjectResult(new BaseResponse<T>
        {
            Data = data,
            Message = message
        });
    }

    [NonAction]
    public ObjectResult RespondSuccessLegacySimple(string message = "", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        Response.StatusCode = (int)statusCode;
        return new ObjectResult(new BaseResponse<object>
        {
            Message = message
        });
    }
}