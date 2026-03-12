using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using MyDhathuru.Application.Common.Exceptions;

namespace MyDhathuru.Api.Filters;

public class ValidationActionFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider;

    public ValidationActionFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new List<string>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_serviceProvider.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContextType = typeof(ValidationContext<>).MakeGenericType(argument.GetType());
            if (Activator.CreateInstance(validationContextType, argument) is not IValidationContext validationContext)
            {
                continue;
            }

            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (result.IsValid)
            {
                continue;
            }

            errors.AddRange(result.Errors.Select(error =>
            {
                var propertyName = error.PropertyName ?? string.Empty;
                var errorMessage = error.ErrorMessage ?? "Validation error";
                return string.IsNullOrWhiteSpace(propertyName) ? errorMessage : $"{propertyName}: {errorMessage}";
            }));
        }

        if (errors.Count > 0)
        {
            throw new AppException("Validation failed.", System.Net.HttpStatusCode.BadRequest)
            {
                Data = { ["Errors"] = errors }
            };
        }

        await next();
    }
}
