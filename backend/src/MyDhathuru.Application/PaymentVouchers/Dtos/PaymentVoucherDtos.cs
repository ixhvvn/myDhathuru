using MyDhathuru.Application.Common.Models;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.PaymentVouchers.Dtos;

public class PaymentVoucherListQuery : PaginationQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public PaymentVoucherStatus? Status { get; set; }
    public Guid? LinkedReceivedInvoiceId { get; set; }
}

public class PaymentVoucherListItemDto
{
    public Guid Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string PayTo { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public PaymentVoucherStatus Status { get; set; }
    public string? Bank { get; set; }
    public string? LinkedReceivedInvoiceNumber { get; set; }
}

public class PaymentVoucherDetailDto
{
    public Guid Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string PayTo { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public string? AccountNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public string? Bank { get; set; }
    public decimal Amount { get; set; }
    public string AmountInWords { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public Guid? LinkedReceivedInvoiceId { get; set; }
    public string? LinkedReceivedInvoiceNumber { get; set; }
    public Guid? LinkedExpenseEntryId { get; set; }
    public string? LinkedExpenseDocumentNumber { get; set; }
    public string? Notes { get; set; }
    public PaymentVoucherStatus Status { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
}

public class CreatePaymentVoucherRequest
{
    public DateOnly Date { get; set; }
    public required string PayTo { get; set; }
    public required string Details { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Transfer;
    public string? AccountNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public string? Bank { get; set; }
    public decimal Amount { get; set; }
    public string? AmountInWords { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public Guid? LinkedReceivedInvoiceId { get; set; }
    public Guid? LinkedExpenseEntryId { get; set; }
    public string? Notes { get; set; }
    public PaymentVoucherStatus Status { get; set; } = PaymentVoucherStatus.Draft;
}

public class UpdatePaymentVoucherRequest : CreatePaymentVoucherRequest
{
}

public class UpdatePaymentVoucherStatusRequest
{
    public string? Notes { get; set; }
}
