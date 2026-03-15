using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Application.DeliveryNotes.Dtos;

public class DeliveryNoteListQuery : PaginationQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? CreatedDatePreset { get; set; }
    public DateOnly? CreatedDateFrom { get; set; }
    public DateOnly? CreatedDateTo { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? VesselId { get; set; }
}

public class DeliveryNoteItemDto
{
    public Guid Id { get; set; }
    public string Details { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
    public decimal CashPayment { get; set; }
    public decimal VesselPayment { get; set; }
}

public class DeliveryNoteListItemDto
{
    public Guid Id { get; set; }
    public string DeliveryNoteNo { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public bool HasPoAttachment { get; set; }
    public string? PoAttachmentFileName { get; set; }
    public DateOnly Date { get; set; }
    public string Currency { get; set; } = "MVR";
    public string Details { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string? Vessel { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
    public string? InvoiceNo { get; set; }
    public decimal CashPayment { get; set; }
    public decimal VesselPayment { get; set; }
    public string? VesselPaymentInvoiceNumber { get; set; }
    public bool HasVesselPaymentInvoiceAttachment { get; set; }
    public string? VesselPaymentInvoiceAttachmentFileName { get; set; }
}

public class DeliveryNoteDetailDto
{
    public Guid Id { get; set; }
    public string DeliveryNoteNo { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public bool HasPoAttachment { get; set; }
    public string? PoAttachmentFileName { get; set; }
    public string? PoAttachmentContentType { get; set; }
    public long? PoAttachmentSizeBytes { get; set; }
    public DateOnly Date { get; set; }
    public string Currency { get; set; } = "MVR";
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid? VesselId { get; set; }
    public string? VesselName { get; set; }
    public string? Notes { get; set; }
    public string? InvoiceNo { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal VesselPaymentFee { get; set; }
    public string? VesselPaymentInvoiceNumber { get; set; }
    public bool HasVesselPaymentInvoiceAttachment { get; set; }
    public string? VesselPaymentInvoiceAttachmentFileName { get; set; }
    public string? VesselPaymentInvoiceAttachmentContentType { get; set; }
    public long? VesselPaymentInvoiceAttachmentSizeBytes { get; set; }
    public decimal TotalAmount { get; set; }
    public List<DeliveryNoteItemDto> Items { get; set; } = new();
}

public class CreateDeliveryNoteRequest
{
    public DateOnly Date { get; set; }
    public string? PoNumber { get; set; }
    public string? Currency { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? VesselId { get; set; }
    public decimal VesselPaymentFee { get; set; }
    public string? VesselPaymentInvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public List<DeliveryNoteItemInputDto> Items { get; set; } = new();
}

public class UpdateDeliveryNoteRequest : CreateDeliveryNoteRequest
{
}

public class DeliveryNoteItemInputDto
{
    public required string Details { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal CashPayment { get; set; }
    public decimal VesselPayment { get; set; }
}

public class CreateInvoiceFromDeliveryNoteRequest
{
    public DateOnly? DateIssued { get; set; }
    public DateOnly? DateDue { get; set; }
    public decimal? TaxRate { get; set; }
    public string? Notes { get; set; }
}

public class CreateInvoiceFromDeliveryNoteResultDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
}

public class DeliveryNoteAttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
}

public class DeliveryNoteAttachmentFileDto : DeliveryNoteAttachmentDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
