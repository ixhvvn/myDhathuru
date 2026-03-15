using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Application.Suppliers.Dtos;

public class SupplierListQuery : PaginationQuery
{
    public bool? IsActive { get; set; }
}

public class SupplierLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SupplierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TinNumber { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int ReceivedInvoiceCount { get; set; }
    public decimal OutstandingAmount { get; set; }
}

public class CreateSupplierRequest
{
    public required string Name { get; set; }
    public string? TinNumber { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSupplierRequest : CreateSupplierRequest
{
}
