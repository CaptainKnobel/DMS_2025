using DMS_2025.REST.DTOs;
using FluentValidation;

namespace DMS_2025.REST.Validation
{
    public sealed class DocumentUploadRequestValidator : AbstractValidator<DocumentUploadRequest>
    {
        private static readonly string[] AllowedContentTypes = {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/tiff"
    };

        private const long MaxBytes = 20 * 1024 * 1024; // 20 MB

        public DocumentUploadRequestValidator()
        {
            RuleFor(x => x.File)
                .NotNull().WithMessage("File is required.")
                .Must(f => f.Length > 0 && f.Length <= MaxBytes)
                    .WithMessage($"File must be >0B and ≤ {MaxBytes / (1024 * 1024)}MB.")
                .Must(f => AllowedContentTypes.Contains(f.ContentType))
                    .WithMessage("Only PDF/PNG/JPG/TIFF allowed.");

            RuleFor(x => x.Title).MaximumLength(255);
            RuleFor(x => x.Location).MaximumLength(255);
            RuleFor(x => x.Author).MaximumLength(255);

            // CreationDate ≤ now
            // RuleFor(x => x.CreationDate).LessThanOrEqualTo(DateTime.UtcNow)
            //     .When(x => x.CreationDate.HasValue);
        }
    }
}
