using MyDhathuru.Domain.Entities;

namespace MyDhathuru.Infrastructure.Extensions;

public static class TenantSettingsFormattingExtensions
{
    public static string BuildCompanyInfo(this TenantSettings settings, bool includeBusinessRegistration = false)
    {
        var parts = new List<string>();

        if (includeBusinessRegistration && !string.IsNullOrWhiteSpace(settings.BusinessRegistrationNumber))
        {
            parts.Add(settings.BusinessRegistrationNumber.Trim());
        }

        if (settings.IsTaxApplicable && !string.IsNullOrWhiteSpace(settings.TinNumber))
        {
            parts.Add($"TIN: {settings.TinNumber.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(settings.CompanyPhone))
        {
            parts.Add($"Phone: {settings.CompanyPhone.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(settings.CompanyEmail))
        {
            parts.Add($"Email: {settings.CompanyEmail.Trim()}");
        }

        return string.Join(", ", parts);
    }
}
