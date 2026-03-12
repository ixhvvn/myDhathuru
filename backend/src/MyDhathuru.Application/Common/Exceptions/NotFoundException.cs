using System.Net;

namespace MyDhathuru.Application.Common.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, HttpStatusCode.NotFound)
    {
    }
}
