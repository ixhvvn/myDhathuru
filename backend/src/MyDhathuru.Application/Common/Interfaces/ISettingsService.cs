using MyDhathuru.Application.Settings.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface ISettingsService
{
    Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<TenantSettingsDto> UpdateAsync(UpdateTenantSettingsRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
}
