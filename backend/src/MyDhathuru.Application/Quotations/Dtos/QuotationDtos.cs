using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Application.Quotations.Dtos;

public class QuotationListQuery : PaginationQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public Guid? CustomerId { get; set; }
}

public class QuotationItemDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
}

public class QuotationItemInputDto
{
    public required string Description { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

public class QuotationListItemDto
{
    public Guid Id { get; set; }
    public string QuotationNo { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public Guid? CourierId { get; set; }
    public string? CourierName { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly ValidUntil { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }
    public string? ConvertedInvoiceNo { get; set; }
}

public class QuotationDetailDto
{
    public Guid Id { get; set; }
    public string QuotationNo { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerTinNumber { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public Guid? CourierId { get; set; }
    public string? CourierName { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }
    public string? ConvertedInvoiceNo { get; set; }
    public List<QuotationItemDto> Items { get; set; } = new();
}

public class QuotationConversionResultDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public bool AlreadyConverted { get; set; }
}

public class CreateQuotationRequest
{
    public Guid CustomerId { get; set; }
    public Guid? CourierId { get; set; }
    public string? PoNumber { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public string? Currency { get; set; }
    public decimal? TaxRate { get; set; }
    public string? Notes { get; set; }
    public List<QuotationItemInputDto> Items { get; set; } = new();
}

public class UpdateQuotationRequest : CreateQuotationRequest
{
}
