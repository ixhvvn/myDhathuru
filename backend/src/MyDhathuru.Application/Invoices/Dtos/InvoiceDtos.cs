using MyDhathuru.Application.Common.Models;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Invoices.Dtos;

public class InvoiceListQuery : PaginationQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? CreatedDatePreset { get; set; }
    public DateOnly? CreatedDateFrom { get; set; }
    public DateOnly? CreatedDateTo { get; set; }
    public Guid? CustomerId { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
}

public class InvoiceItemDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
}

public class InvoiceItemInputDto
{
    public required string Description { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

public class InvoicePaymentDto
{
    public Guid Id { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class InvoiceListItemDto
{
    public Guid Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public Guid? QuotationId { get; set; }
    public string? QuotationNo { get; set; }
    public string Customer { get; set; } = string.Empty;
    public Guid? CourierId { get; set; }
    public string? CourierName { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly DateDue { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DocumentEmailStatus EmailStatus { get; set; }
    public DateTimeOffset? LastEmailedAt { get; set; }
}

public class InvoiceDetailDto
{
    public Guid Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public Guid? QuotationId { get; set; }
    public string? QuotationNo { get; set; }
    public string? PoNumber { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerTinNumber { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public Guid? DeliveryNoteId { get; set; }
    public string? DeliveryNoteNo { get; set; }
    public Guid? CourierId { get; set; }
    public string? CourierName { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly DateDue { get; set; }
    public string Currency { get; set; } = "MVR";

    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DocumentEmailStatus EmailStatus { get; set; }
    public DateTimeOffset? LastEmailedAt { get; set; }
    public string? Notes { get; set; }

    public List<InvoiceItemDto> Items { get; set; } = new();
    public List<InvoicePaymentDto> Payments { get; set; } = new();
}

public class InvoiceBankDetailsDto
{
    public string BmlMvrAccountName { get; set; } = string.Empty;
    public string BmlMvrAccountNumber { get; set; } = string.Empty;
    public string BmlUsdAccountName { get; set; } = string.Empty;
    public string BmlUsdAccountNumber { get; set; } = string.Empty;
    public string MibMvrAccountName { get; set; } = string.Empty;
    public string MibMvrAccountNumber { get; set; } = string.Empty;
    public string MibUsdAccountName { get; set; } = string.Empty;
    public string MibUsdAccountNumber { get; set; } = string.Empty;
    public string InvoiceOwnerName { get; set; } = string.Empty;
    public string InvoiceOwnerIdCard { get; set; } = string.Empty;
}

public class CreateInvoiceRequest
{
    public Guid CustomerId { get; set; }
    public Guid? DeliveryNoteId { get; set; }
    public Guid? CourierId { get; set; }
    public string? PoNumber { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly? DateDue { get; set; }
    public string? Currency { get; set; }
    public decimal? TaxRate { get; set; }
    public string? Notes { get; set; }
    public List<InvoiceItemInputDto> Items { get; set; } = new();
}

public class UpdateInvoiceRequest : CreateInvoiceRequest
{
}

public class ReceiveInvoicePaymentRequest
{
    public string? Currency { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}

public class SendInvoiceEmailRequest
{
    public string? CcEmail { get; set; }
    public string? Body { get; set; }
}
