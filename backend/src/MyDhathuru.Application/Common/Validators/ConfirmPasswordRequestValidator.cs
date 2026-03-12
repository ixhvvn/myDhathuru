using FluentValidation;
using MyDhathuru.Application.Common.Dtos;

namespace MyDhathuru.Application.Common.Validators;

public class ConfirmPasswordRequestValidator : AbstractValidator<ConfirmPasswordRequest>
{
    public ConfirmPasswordRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}
