using MyDhathuru.Application.Common.Models;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.ReceivedInvoices.Dtos;

public class ReceivedInvoiceListQuery : PaginationQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? ExpenseCategoryId { get; set; }
    public ReceivedInvoiceStatus? PaymentStatus { get; set; }
    public ApprovalStatus? ApprovalStatus { get; set; }
    public bool? IsTaxClaimable { get; set; }
    public bool OverdueOnly { get; set; }
}

public class ReceivedInvoiceItemDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Uom { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal GstRate { get; set; }
    public decimal GstAmount { get; set; }
}

public class ReceivedInvoiceItemInputDto
{
    public required string Description { get; set; }
    public string? Uom { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GstRate { get; set; }
}

public class ReceivedInvoicePaymentDto
{
    public Guid Id { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid? PaymentVoucherId { get; set; }
    public string? PaymentVoucherNumber { get; set; }
}

public class ReceivedInvoiceAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public class ReceivedInvoiceAttachmentFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public class ReceivedInvoiceListItemDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal TotalAmount { get; set; }
    public decimal BalanceDue { get; set; }
    public ReceivedInvoiceStatus PaymentStatus { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public string ExpenseCategoryName { get; set; } = string.Empty;
    public bool IsTaxClaimable { get; set; }
    public bool IsOverdue { get; set; }
    public int AttachmentCount { get; set; }
}

public class ReceivedInvoiceDetailDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierTin { get; set; }
    public string? SupplierContactNumber { get; set; }
    public string? SupplierEmail { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Outlet { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GstRate { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BalanceDue { get; set; }
    public ReceivedInvoiceStatus PaymentStatus { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? ReceiptReference { get; set; }
    public string? SettlementReference { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountDetails { get; set; }
    public string? MiraTaxableActivityNumber { get; set; }
    public RevenueCapitalType RevenueCapitalClassification { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public string ExpenseCategoryName { get; set; } = string.Empty;
    public bool IsTaxClaimable { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public List<ReceivedInvoiceItemDto> Items { get; set; } = new();
    public List<ReceivedInvoicePaymentDto> Payments { get; set; } = new();
    public List<ReceivedInvoiceAttachmentDto> Attachments { get; set; } = new();
}

public class CreateReceivedInvoiceRequest
{
    public Guid SupplierId { get; set; }
    public required string InvoiceNumber { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Outlet { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? Currency { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal? GstRate { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? ReceiptReference { get; set; }
    public string? SettlementReference { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountDetails { get; set; }
    public string? MiraTaxableActivityNumber { get; set; }
    public RevenueCapitalType RevenueCapitalClassification { get; set; } = RevenueCapitalType.Revenue;
    public Guid ExpenseCategoryId { get; set; }
    public bool IsTaxClaimable { get; set; } = true;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public List<ReceivedInvoiceItemInputDto> Items { get; set; } = new();
}

public class UpdateReceivedInvoiceRequest : CreateReceivedInvoiceRequest
{
}

public class RecordReceivedInvoicePaymentRequest
{
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid? PaymentVoucherId { get; set; }
}
