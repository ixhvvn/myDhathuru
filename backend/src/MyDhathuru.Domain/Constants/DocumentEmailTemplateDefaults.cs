using System.Text.RegularExpressions;

namespace MyDhathuru.Domain.Constants;

public static class DocumentEmailTemplateDefaults
{
    private const string LegacyQuotation = """
Dear Sir/Madam,

Kindly find the attached Quotation for your reference from {{companyName}}

Thanks and regards,
{{companyName}}.
""";

    private const string LegacyInvoice = """
Dear Sir/Madam,

Kindly find the attached Invoice for your reference from {{companyName}}

Thanks and regards,
{{companyName}}.
""";

    private const string LegacyPurchaseOrder = """
Dear Sir/Madam,

Kindly find the attached PO for your reference from {{companyName}}

Thanks and regards,
{{companyName}}.
""";

    public const string Quotation = """
Dear Sir/Madam,

Kindly find the attached Quotation for your reference from {{companyName}}.

Thanks and regards,
{{companyName}},
""";

    public const string Invoice = """
Dear Sir/Madam,

Kindly find the attached Invoice for your reference from {{companyName}}.

Thanks and regards,
{{companyName}},
""";

    public const string PurchaseOrder = """
Dear Sir/Madam,

Kindly find the attached PO for your reference from {{companyName}}.

Thanks and regards,
{{companyName}},
""";

    public static string NormalizeQuotation(string? template) => NormalizeTemplate(template, Quotation, LegacyQuotation);

    public static string NormalizeInvoice(string? template) => NormalizeTemplate(template, Invoice, LegacyInvoice);

    public static string NormalizePurchaseOrder(string? template) => NormalizeTemplate(template, PurchaseOrder, LegacyPurchaseOrder);

    private static string NormalizeTemplate(string? template, string fallback, string legacyTemplate)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return fallback;
        }

        var normalized = template
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();

        normalized = NormalizeSignoff(normalized);

        return normalized == legacyTemplate
            ? fallback
            : normalized;
    }

    private static string NormalizeSignoff(string template)
    {
        var normalized = Regex.Replace(
            template,
            @"(^|\n)(Thanks and regards,)\s+([^\n]+)(?=\n|$)",
            "$1$2\n$3",
            RegexOptions.IgnoreCase);

        return Regex.Replace(
            normalized,
            @"(^|\n)(Thanks & regards,)\s+([^\n]+)(?=\n|$)",
            "$1$2\n$3",
            RegexOptions.IgnoreCase);
    }
}
