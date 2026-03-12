using MyDhathuru.Application.Statements.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IStatementService
{
    Task<AccountStatementDto> GetStatementAsync(Guid customerId, int year, CancellationToken cancellationToken = default);
    Task SaveOpeningBalanceAsync(SaveOpeningBalanceRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePdfAsync(Guid customerId, int year, CancellationToken cancellationToken = default);
}
