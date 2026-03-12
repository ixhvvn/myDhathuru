using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Application.Common.Interfaces;

public interface ICurrentUserService
{
    RequestContext GetContext();
}
