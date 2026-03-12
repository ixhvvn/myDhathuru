using System.Net;

namespace MyDhathuru.Application.Common.Exceptions;

public class AppException : Exception
{
    public AppException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest) : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
