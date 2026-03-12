using System.Net;

namespace MyDhathuru.Application.Common.Exceptions;

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, HttpStatusCode.Forbidden)
    {
    }
}
