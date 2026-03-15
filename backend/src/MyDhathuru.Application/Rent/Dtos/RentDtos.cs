using MyDhathuru.Application.Common.Models;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Rent.Dtos;

public class RentEntryListQuery : PaginationQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public Guid? ExpenseCategoryId { get; set; }
}

public class RentEntryListItemDto
{
    public Guid Id { get; set; }
    public string RentNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string PayTo { get; set; } = string.Empty;
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public string ExpenseCategoryName { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; }
}

public class RentEntryDetailDto : RentEntryListItemDto
{
    public string? Notes { get; set; }
}

public class CreateRentEntryRequest
{
    public DateOnly Date { get; set; }
    public required string PropertyName { get; set; }
    public required string PayTo { get; set; }
    public string? Currency { get; set; }
    public decimal Amount { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public string? Notes { get; set; }
}

public class UpdateRentEntryRequest : CreateRentEntryRequest
{
}
