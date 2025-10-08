using System;
using System.Linq;
using DMS_2025.REST.DTOs;
using DMS_2025.REST.Validation;
using FluentValidation.TestHelper;
using NUnit.Framework;

namespace DMS_2025.Tests.REST
{
    public class DocumentCreateRequestValidatorTests
    {
        [Test]
        public void Title_Is_Optional_NoError_When_Null()
        {
            var validator = new DocumentCreateRequestValidator();
            var model = new DocumentCreateRequest { Title = null, Location = "/x", Author = "a" };

            var result = validator.TestValidate(model);
            result.ShouldNotHaveValidationErrorFor(x => x.Title);
        }

        [Test]
        public void Title_TooLong_Fails()
        {
            var validator = new DocumentCreateRequestValidator();
            var model = new DocumentCreateRequest { Title = new string('A', 256) };

            var result = validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Test]
        public void Location_TooLong_Fails()
        {
            var validator = new DocumentCreateRequestValidator();
            var model = new DocumentCreateRequest { Location = new string('B', 256) };

            var result = validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Location);
        }

        [Test]
        public void Author_TooLong_Fails()
        {
            var validator = new DocumentCreateRequestValidator();
            var model = new DocumentCreateRequest { Author = new string('C', 256) };

            var result = validator.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.Author);
        }

        [Test]
        public void Valid_Minimal_Request_Has_No_Errors()
        {
            var validator = new DocumentCreateRequestValidator();
            var model = new DocumentCreateRequest
            {
                // Title optional
                Location = "/file.pdf",
                Author = "alice"
                // CreationDate optional
            };

            var result = validator.TestValidate(model);
            Assert.That(result.Errors, Is.Empty);
        }

        // if later CreationDate rule in the validator is added:
        // [Test]
        // public void CreationDate_In_Future_Fails()
        // {
        //     var validator = new DocumentCreateRequestValidator();
        //     var model = new DocumentCreateRequest { CreationDate = DateTime.UtcNow.AddMinutes(5) };
        //     var result = validator.TestValidate(model);
        //     result.ShouldHaveValidationErrorFor(x => x.CreationDate);
        // }
    }
}
