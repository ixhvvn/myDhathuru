using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IDocumentNumberService
{
    Task<string> GenerateAsync(DocumentType documentType, DateOnly date, CancellationToken cancellationToken = default);
}
