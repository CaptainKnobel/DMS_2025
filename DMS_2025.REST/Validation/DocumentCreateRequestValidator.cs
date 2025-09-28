using DMS_2025.REST.DTOs;
using FluentValidation;

namespace DMS_2025.REST.Validation
{
    public sealed class DocumentCreateRequestValidator : AbstractValidator<DocumentCreateRequest>
    {
        public DocumentCreateRequestValidator()
        {
            RuleFor(x => x.Title).MaximumLength(255);
            RuleFor(x => x.Location).MaximumLength(255);
            RuleFor(x => x.Author).MaximumLength(255);

            // CreationDate?
            // RuleFor(x => x.CreationDate).LessThanOrEqualTo(DateTime.UtcNow).When(x => x.CreationDate.HasValue);
        }
    }
}
