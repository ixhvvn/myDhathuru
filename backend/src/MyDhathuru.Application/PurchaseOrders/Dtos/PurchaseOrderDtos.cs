using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Application.PurchaseOrders.Dtos;

public class PurchaseOrderListQuery : PaginationQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public Guid? SupplierId { get; set; }
}

public class PurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
}

public class PurchaseOrderItemInputDto
{
    public required string Description { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}

public class PurchaseOrderListItemDto
{
    public Guid Id { get; set; }
    public string PurchaseOrderNo { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public Guid? CourierId { get; set; }
    public string? CourierName { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly RequiredDate { get; set; }
}

public class PurchaseOrderDetailDto
{
    public Guid Id { get; set; }
    public string PurchaseOrderNo { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierTinNumber { get; set; }
    public string? SupplierContactNumber { get; set; }
    public string? SupplierEmail { get; set; }
    public Guid? CourierId { get; set; }
    public string? CourierName { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly RequiredDate { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = new();
}

public class CreatePurchaseOrderRequest
{
    public Guid SupplierId { get; set; }
    public Guid? CourierId { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly? RequiredDate { get; set; }
    public string? Currency { get; set; }
    public decimal? TaxRate { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderItemInputDto> Items { get; set; } = new();
}

public class UpdatePurchaseOrderRequest : CreatePurchaseOrderRequest
{
}
