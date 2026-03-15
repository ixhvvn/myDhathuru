using FluentValidation;
using MyDhathuru.Application.Expenses.Dtos;

namespace MyDhathuru.Application.Expenses.Validators;

public class CreateExpenseCategoryRequestValidator : AbstractValidator<CreateExpenseCategoryRequest>
{
    public CreateExpenseCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Description).MaximumLength(400);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 999);
    }
}

public class UpdateExpenseCategoryRequestValidator : AbstractValidator<UpdateExpenseCategoryRequest>
{
    public UpdateExpenseCategoryRequestValidator()
    {
        Include(new CreateExpenseCategoryRequestValidator());
    }
}
