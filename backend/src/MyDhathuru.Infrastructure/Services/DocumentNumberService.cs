using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class DocumentNumberService : IDocumentNumberService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    public DocumentNumberService(ApplicationDbContext dbContext, ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
    }

    public async Task<string> GenerateAsync(DocumentType documentType, DateOnly date, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is required.");
        var year = date.Year;

        var settings = await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new AppException("Tenant settings not found.");

        var prefix = documentType switch
        {
            DocumentType.DeliveryNote => settings.DeliveryNotePrefix,
            DocumentType.Invoice => settings.InvoicePrefix,
            DocumentType.Statement => settings.StatementPrefix,
            DocumentType.SalarySlip => settings.SalarySlipPrefix,
            _ => "DOC"
        };

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sequence = await _dbContext.DocumentSequences
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.DocumentType == documentType && x.Year == year, cancellationToken);

        int current;
        if (sequence is null)
        {
            sequence = new DocumentSequence
            {
                TenantId = tenantId,
                DocumentType = documentType,
                Year = year,
                NextNumber = 2
            };
            current = 1;
            _dbContext.DocumentSequences.Add(sequence);
        }
        else
        {
            current = sequence.NextNumber;
            sequence.NextNumber += 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return $"{prefix}-{year}-{current:000}";
    }
}
