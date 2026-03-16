namespace MyDhathuru.Domain.Constants;

public static class DocumentEmailTemplateDefaults
{
    public const string Quotation = """
Dear Sir/Madam,

Kindly find the attached Quotation for your reference from {{companyName}}

Thanks and regards,
{{companyName}}.
""";

    public const string Invoice = """
Dear Sir/Madam,

Kindly find the attached Invoice for your reference from {{companyName}}

Thanks and regards,
{{companyName}}.
""";

    public const string PurchaseOrder = """
Dear Sir/Madam,

Kindly find the attached PO for your reference from {{companyName}}

Thanks and regards,
{{companyName}}.
""";
}
