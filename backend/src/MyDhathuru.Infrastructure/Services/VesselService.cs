using Microsoft.EntityFrameworkCore;
using Npgsql;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Customers.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class VesselService : IVesselService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;

    public VesselService(ApplicationDbContext dbContext, ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
    }

    public async Task<PagedResult<VesselDto>> GetPagedAsync(VesselListQuery query, CancellationToken cancellationToken = default)
    {
        var vesselsQuery = _dbContext.Vessels
            .AsNoTracking()
            .Where(x =>
                string.IsNullOrWhiteSpace(query.Search)
                || x.Name.ToLower().Contains(query.Search.ToLower())
                || (x.RegistrationNumber != null && x.RegistrationNumber.ToLower().Contains(query.Search.ToLower()))
                || (x.VesselType != null && x.VesselType.ToLower().Contains(query.Search.ToLower()))
                || (x.OwnerName != null && x.OwnerName.ToLower().Contains(query.Search.ToLower()))
                || (x.HomePort != null && x.HomePort.ToLower().Contains(query.Search.ToLower())));

        vesselsQuery = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? vesselsQuery.OrderBy(x => x.Name)
            : vesselsQuery.OrderByDescending(x => x.CreatedAt);

        return await vesselsQuery
            .Select(x => new VesselDto
            {
                Id = x.Id,
                Name = x.Name,
                RegistrationNumber = x.RegistrationNumber,
                IssuedDate = x.IssuedDate,
                PassengerCapacity = x.PassengerCapacity,
                VesselType = x.VesselType,
                HomePort = x.HomePort,
                OwnerName = x.OwnerName,
                ContactPhone = x.ContactPhone,
                Notes = x.Notes
            })
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<VesselDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vessels
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new VesselDto
            {
                Id = x.Id,
                Name = x.Name,
                RegistrationNumber = x.RegistrationNumber,
                IssuedDate = x.IssuedDate,
                PassengerCapacity = x.PassengerCapacity,
                VesselType = x.VesselType,
                HomePort = x.HomePort,
                OwnerName = x.OwnerName,
                ContactPhone = x.ContactPhone,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<VesselDto> CreateAsync(CreateVesselRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new AppException("Tenant context is missing.");
        var normalized = NormalizeRequest(request);

        var existingByName = await _dbContext.Vessels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == normalized.Name, cancellationToken);

        Vessel? existingByRegistration = null;
        if (!string.IsNullOrWhiteSpace(normalized.RegistrationNumber))
        {
            existingByRegistration = await _dbContext.Vessels
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.RegistrationNumber == normalized.RegistrationNumber,
                    cancellationToken);
        }

        if (existingByName is not null && existingByRegistration is not null && existingByName.Id != existingByRegistration.Id)
        {
            throw new AppException("Vessel name and registration number conflict with existing records.");
        }

        var existing = existingByRegistration ?? existingByName;
        if (existing is not null)
        {
            if (!existing.IsDeleted)
            {
                throw new AppException("Vessel already exists.");
            }

            ApplyRequest(existing, normalized);
            existing.IsDeleted = false;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return MapVessel(existing);
        }

        var vessel = new Vessel
        {
            Name = normalized.Name,
            TenantId = tenantId
        };

        ApplyRequest(vessel, normalized);
        _dbContext.Vessels.Add(vessel);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               {
                   SqlState: PostgresErrorCodes.UniqueViolation,
                   ConstraintName: "IX_Vessels_TenantId_Name" or "IX_Vessels_TenantId_RegistrationNumber"
               })
        {
            throw new AppException("Vessel name or registration number already exists.");
        }

        return MapVessel(vessel);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vessel = await _dbContext.Vessels.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Vessel not found.");

        var isUsed = await _dbContext.DeliveryNotes.AnyAsync(x => x.VesselId == id, cancellationToken);
        if (isUsed)
        {
            throw new AppException("Cannot delete vessel with linked delivery notes.");
        }

        _dbContext.Vessels.Remove(vessel);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static VesselDto MapVessel(Vessel vessel)
    {
        return new VesselDto
        {
            Id = vessel.Id,
            Name = vessel.Name,
            RegistrationNumber = vessel.RegistrationNumber,
            IssuedDate = vessel.IssuedDate,
            PassengerCapacity = vessel.PassengerCapacity,
            VesselType = vessel.VesselType,
            HomePort = vessel.HomePort,
            OwnerName = vessel.OwnerName,
            ContactPhone = vessel.ContactPhone,
            Notes = vessel.Notes
        };
    }

    private static VesselRequestValues NormalizeRequest(CreateVesselRequest request)
    {
        return new VesselRequestValues(
            request.Name.Trim(),
            NormalizeOptional(request.RegistrationNumber),
            request.IssuedDate,
            request.PassengerCapacity,
            NormalizeOptional(request.VesselType),
            NormalizeOptional(request.HomePort),
            NormalizeOptional(request.OwnerName),
            NormalizeOptional(request.ContactPhone),
            NormalizeOptional(request.Notes));
    }

    private static void ApplyRequest(Vessel vessel, VesselRequestValues values)
    {
        vessel.Name = values.Name;
        vessel.RegistrationNumber = values.RegistrationNumber;
        vessel.IssuedDate = values.IssuedDate;
        vessel.PassengerCapacity = values.PassengerCapacity;
        vessel.VesselType = values.VesselType;
        vessel.HomePort = values.HomePort;
        vessel.OwnerName = values.OwnerName;
        vessel.ContactPhone = values.ContactPhone;
        vessel.Notes = values.Notes;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private record VesselRequestValues(
        string Name,
        string? RegistrationNumber,
        DateOnly? IssuedDate,
        int? PassengerCapacity,
        string? VesselType,
        string? HomePort,
        string? OwnerName,
        string? ContactPhone,
        string? Notes);
}
