using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DMS_2025.REST.Validation
{
    public sealed class FluentValidationActionFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var sp = context.HttpContext.RequestServices;

            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg is null) continue;

                // locate IValidator<arg.GetType()>
                var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
                if (sp.GetService(validatorType) is not IValidator validator) continue;

                ValidationResult result = await validator.ValidateAsync(new ValidationContext<object>(arg));
                if (!result.IsValid)
                {
                    foreach (var failure in result.Errors)
                    {
                        // Key must match property name for ProblemDetails.errors mapping
                        context.ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
                    }
                }
            }

            if (!context.ModelState.IsValid)
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred."
                };

                context.Result = new ObjectResult(problem)
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
                return;
            }

            await next();
        }
    }
}
