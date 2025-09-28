using DMS_2025.REST.DTOs;
using FluentValidation;

namespace DMS_2025.REST.Validation
{
    public sealed class DocumentUpdateRequestValidator : AbstractValidator<DocumentUpdateRequest>
    {
        public DocumentUpdateRequestValidator()
        {
            RuleFor(x => x.Title).MaximumLength(255).When(x => x.Title != null);
            RuleFor(x => x.Location).MaximumLength(255).When(x => x.Location != null);
            RuleFor(x => x.Author).MaximumLength(255).When(x => x.Author != null);
        }
    }
}
