namespace MyDhathuru.Domain.Enums;

public enum AdminAuditActionType
{
    SignupRequestApproved = 1,
    SignupRequestRejected = 2,
    BusinessDisabled = 3,
    BusinessEnabled = 4,
    BusinessLoginUpdated = 5,
    BusinessPasswordResetSent = 6,
    BillingInvoiceGenerated = 7,
    BillingInvoiceBulkGenerated = 8,
    BillingInvoiceCustomGenerated = 9,
    BillingInvoiceEmailed = 10,
    BillingSettingsUpdated = 11,
    BillingCustomRateCreated = 12,
    BillingCustomRateUpdated = 13,
    BillingCustomRateDeleted = 14,
    BillingInvoicesReset = 15,
    EmailCampaignSent = 16
}
