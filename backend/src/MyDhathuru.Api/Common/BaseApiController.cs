using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Api.Common;

[ApiController]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponse<T>> OkResponse<T>(T data, string message = "Success")
    {
        return Ok(ApiResponse<T>.Ok(data, message));
    }

    protected ActionResult<ApiResponse<object>> SuccessMessage(string message)
    {
        return Ok(ApiResponse<object>.Ok(new { }, message));
    }
}
