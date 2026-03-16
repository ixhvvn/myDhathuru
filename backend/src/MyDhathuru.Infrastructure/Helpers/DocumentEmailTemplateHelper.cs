using MyDhathuru.Domain.Constants;

namespace MyDhathuru.Infrastructure.Helpers;

internal static class DocumentEmailTemplateHelper
{
    public static string RenderQuotation(string? template, string companyName) =>
        Render(template, DocumentEmailTemplateDefaults.Quotation, companyName);

    public static string RenderInvoice(string? template, string companyName) =>
        Render(template, DocumentEmailTemplateDefaults.Invoice, companyName);

    public static string RenderPurchaseOrder(string? template, string companyName) =>
        Render(template, DocumentEmailTemplateDefaults.PurchaseOrder, companyName);

    private static string Render(string? template, string fallbackTemplate, string companyName)
    {
        var resolvedCompanyName = string.IsNullOrWhiteSpace(companyName)
            ? "myDhathuru"
            : companyName.Trim();

        var source = string.IsNullOrWhiteSpace(template)
            ? fallbackTemplate
            : template.Trim();

        return source
            .Replace("{{companyName}}", resolvedCompanyName, StringComparison.OrdinalIgnoreCase)
            .Replace("[User company name]", resolvedCompanyName, StringComparison.OrdinalIgnoreCase)
            .Replace("[User Company]", resolvedCompanyName, StringComparison.OrdinalIgnoreCase);
    }
}
