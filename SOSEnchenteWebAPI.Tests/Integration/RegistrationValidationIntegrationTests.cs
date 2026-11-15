using Xunit;
using SOSEnchenteWebAPI.Services;

namespace SOSEnchenteWebAPI.Tests.Integration;

/// <summary>
/// Integration tests for email and phone validation in registration scenarios
/// </summary>
public class RegistrationValidationIntegrationTests
{
    private readonly ValidationService _validationService;

    public RegistrationValidationIntegrationTests()
    {
        _validationService = new ValidationService();
    }

    [Fact]
    public void RegistrationValidation_WithValidUserData_PassesAllValidation()
    {
        // Arrange
        string email = "newuser@example.com";
        string phoneNumber = "(11) 99999-9999";

        // Act
        bool emailValid = _validationService.IsValidEmail(email);
        bool phoneValid = _validationService.IsValidPhoneNumber(phoneNumber);

        // Assert
        Assert.True(emailValid, "Email should be valid");
        Assert.True(phoneValid, "Phone number should be valid");
    }

    [Fact]
    public void RegistrationValidation_WithBrazilianPhoneFormats_AllPassValidation()
    {
        // Arrange & Act & Assert
        var validPhones = new[]
        {
            "(11) 99999-9999",    // Standard format
            "11 99999-9999",      // Without parentheses
            "11999999999",        // Without formatting
            "+55 11 99999-9999",  // International
            "+5511999999999"      // International without spaces
        };

        foreach (var phone in validPhones)
        {
            bool result = _validationService.IsValidPhoneNumber(phone);
            Assert.True(result, $"Phone '{phone}' should be valid");
        }
    }

    [Fact]
    public void RegistrationValidation_WithCommonEmailFormats_AllPassValidation()
    {
        // Arrange & Act & Assert
        var validEmails = new[]
        {
            "user@example.com",
            "john.doe@company.co.uk",
            "alice+tag@domain.org",
            "test123@test-domain.com",
            "a@b.co"
        };

        foreach (var email in validEmails)
        {
            bool result = _validationService.IsValidEmail(email);
            Assert.True(result, $"Email '{email}' should be valid");
        }
    }

    [Fact]
    public void RegistrationValidation_WithInvalidEmailFormats_AllFailValidation()
    {
        // Arrange & Act & Assert
        var invalidEmails = new[]
        {
            "notanemail",           // No @
            "user@",                // No domain
            "@example.com",         // No local part
            "user@@example.com",    // Double @
            "user@example",         // No extension
            ".user@example.com",    // Starts with dot
            "user..name@example.com" // Consecutive dots
        };

        foreach (var email in invalidEmails)
        {
            bool result = _validationService.IsValidEmail(email);
            Assert.False(result, $"Email '{email}' should be invalid");
        }
    }

    [Fact]
    public void RegistrationValidation_WithInvalidPhoneFormats_AllFailValidation()
    {
        // Arrange & Act & Assert
        var invalidPhones = new[]
        {
            "123",                  // Too few digits
            "12345",                // Too few digits
            "123456789012345678",   // Too many digits
            "abcdefghij",           // Non-numeric
            "abc-def-ghij",         // All letters
            "   ",                  // Only spaces
            "@@@@@@"                // Special characters only
        };

        foreach (var phone in invalidPhones)
        {
            bool result = _validationService.IsValidPhoneNumber(phone);
            Assert.False(result, $"Phone '{phone}' should be invalid");
        }
    }

    [Fact]
    public void RegistrationValidation_EmailValidationBoundaryCase_Length254Passes()
    {
        // Arrange - Create an email exactly 254 characters long (maximum allowed)
        string localPart = new string('a', 64); // Max 64 characters
        string domain = new string('b', 185) + ".com"; // Rest for domain to reach exactly 254
        string email = $"{localPart}@{domain}";

        // Act
        bool result = _validationService.IsValidEmail(email);

        // Assert
        Assert.True(result, "Email at maximum length (254 chars) should be valid");
    }

    [Fact]
    public void RegistrationValidation_EmailValidationBoundaryCase_Length255Fails()
    {
        // Arrange - Create an email over 254 characters (exceeds maximum)
        string email = new string('a', 250) + "@example.com";

        // Act
        bool result = _validationService.IsValidEmail(email);

        // Assert
        Assert.False(result, "Email exceeding 254 chars should be invalid");
    }

    [Fact]
    public void RegistrationValidation_PhoneValidationBoundaryCase_8DigitsPasses()
    {
        // Arrange - Minimum valid phone length
        string phone = "12345678";

        // Act
        bool result = _validationService.IsValidPhoneNumber(phone);

        // Assert
        Assert.True(result, "Phone with minimum 8 digits should be valid");
    }

    [Fact]
    public void RegistrationValidation_PhoneValidationBoundaryCase_15DigitsPasses()
    {
        // Arrange - Maximum valid phone length
        string phone = "123456789012345";

        // Act
        bool result = _validationService.IsValidPhoneNumber(phone);

        // Assert
        Assert.True(result, "Phone with maximum 15 digits should be valid");
    }

    [Fact]
    public void RegistrationValidation_PhoneValidationBoundaryCase_7DigitsFails()
    {
        // Arrange - Below minimum phone length
        string phone = "1234567";

        // Act
        bool result = _validationService.IsValidPhoneNumber(phone);

        // Assert
        Assert.False(result, "Phone with less than 8 digits should be invalid");
    }

    [Fact]
    public void RegistrationValidation_PhoneValidationBoundaryCase_16DigitsFails()
    {
        // Arrange - Above maximum phone length
        string phone = "1234567890123456";

        // Act
        bool result = _validationService.IsValidPhoneNumber(phone);

        // Assert
        Assert.False(result, "Phone with more than 15 digits should be invalid");
    }

    [Fact]
    public void RegistrationValidation_ErrorMessages_AreNotEmpty()
    {
        // Act
        string emailErrorMsg = _validationService.GetEmailValidationErrorMessage();
        string phoneErrorMsg = _validationService.GetPhoneNumberValidationErrorMessage();

        // Assert
        Assert.NotEmpty(emailErrorMsg);
        Assert.NotEmpty(phoneErrorMsg);
        Assert.Contains("Email", emailErrorMsg);
        Assert.Contains("Número", phoneErrorMsg);
    }

    [Theory]
    [InlineData("valid@email.com", "(11) 98765-4321")]
    [InlineData("user.name@domain.org", "21999887766")]
    [InlineData("test+tag@example.co.uk", "+55 21 3333-4444")]
    public void RegistrationValidation_WithRealWorldScenarios_AllValidationsPasses(
        string email,
        string phoneNumber)
    {
        // Act
        bool emailValid = _validationService.IsValidEmail(email);
        bool phoneValid = _validationService.IsValidPhoneNumber(phoneNumber);

        // Assert
        Assert.True(emailValid && phoneValid,
            $"Real-world scenario should pass for email '{email}' and phone '{phoneNumber}'");
    }
}
