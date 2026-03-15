using MyDhathuru.Application.Common.Models;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Expenses.Dtos;

public class ExpenseCategoryListQuery : PaginationQuery
{
    public bool? IsActive { get; set; }
}

public class ExpenseCategoryLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public BptCategoryCode BptCategoryCode { get; set; }
}

public class ExpenseCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public BptCategoryCode BptCategoryCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }
    public int UsageCount { get; set; }
}

public class CreateExpenseCategoryRequest
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
    public BptCategoryCode BptCategoryCode { get; set; } = BptCategoryCode.Other;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UpdateExpenseCategoryRequest : CreateExpenseCategoryRequest
{
}
