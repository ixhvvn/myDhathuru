using System.Net;

namespace MyDhathuru.Application.Common.Exceptions;

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message, HttpStatusCode.Unauthorized)
    {
    }
}
