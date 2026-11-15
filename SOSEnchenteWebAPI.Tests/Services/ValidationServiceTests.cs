using Xunit;
using SOSEnchenteWebAPI.Services;

namespace SOSEnchenteWebAPI.Tests.Services;

public class ValidationServiceTests
{
    private readonly ValidationService _validationService;

    public ValidationServiceTests()
    {
        _validationService = new ValidationService();
    }

    #region Email Validation Tests

    [Fact]
    public void IsValidEmail_WithValidEmail_ReturnsTrue()
    {
        // Arrange
        string validEmail = "user@example.com";

        // Act
        bool result = _validationService.IsValidEmail(validEmail);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidEmail_WithValidComplexEmail_ReturnsTrue()
    {
        // Arrange
        string validEmail = "john.doe+tag@example.co.uk";

        // Act
        bool result = _validationService.IsValidEmail(validEmail);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidEmail_WithNullEmail_ReturnsFalse()
    {
        // Arrange
        string? nullEmail = null;

        // Act
        bool result = _validationService.IsValidEmail(nullEmail!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithEmptyEmail_ReturnsFalse()
    {
        // Arrange
        string emptyEmail = string.Empty;

        // Act
        bool result = _validationService.IsValidEmail(emptyEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithWhitespaceEmail_ReturnsFalse()
    {
        // Arrange
        string whitespaceEmail = "   ";

        // Act
        bool result = _validationService.IsValidEmail(whitespaceEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithEmailMissingAtSymbol_ReturnsFalse()
    {
        // Arrange
        string invalidEmail = "userexample.com";

        // Act
        bool result = _validationService.IsValidEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithEmailMissingDomain_ReturnsFalse()
    {
        // Arrange
        string invalidEmail = "user@";

        // Act
        bool result = _validationService.IsValidEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithEmailMissingExtension_ReturnsFalse()
    {
        // Arrange
        string invalidEmail = "user@example";

        // Act
        bool result = _validationService.IsValidEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithEmailStartingWithDot_ReturnsFalse()
    {
        // Arrange
        string invalidEmail = ".user@example.com";

        // Act
        bool result = _validationService.IsValidEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithEmailEndingWithDot_ReturnsFalse()
    {
        // Arrange
        string invalidEmail = "user@example.";

        // Act
        bool result = _validationService.IsValidEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithConsecutiveDots_ReturnsFalse()
    {
        // Arrange
        string invalidEmail = "user..name@example.com";

        // Act
        bool result = _validationService.IsValidEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithEmailTooLong_ReturnsFalse()
    {
        // Arrange
        string invalidEmail = new string('a', 250) + "@example.com";

        // Act
        bool result = _validationService.IsValidEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidEmail_WithMultipleAtSymbols_ReturnsFalse()
    {
        // Arrange
        string invalidEmail = "user@@example.com";

        // Act
        bool result = _validationService.IsValidEmail(invalidEmail);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("test@domain.com")]
    [InlineData("user.name@domain.co.uk")]
    [InlineData("firstname+lastname@domain.com")]
    [InlineData("a@b.co")]
    public void IsValidEmail_WithVariousValidEmails_ReturnsTrue(string email)
    {
        // Act
        bool result = _validationService.IsValidEmail(email);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Phone Number Validation Tests

    [Fact]
    public void IsValidPhoneNumber_WithValidBrazilianPhone_ReturnsTrue()
    {
        // Arrange
        string validPhone = "(11) 99999-9999";

        // Act
        bool result = _validationService.IsValidPhoneNumber(validPhone);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidPhoneNumber_WithValidPhoneWithoutFormatting_ReturnsTrue()
    {
        // Arrange
        string validPhone = "11999999999";

        // Act
        bool result = _validationService.IsValidPhoneNumber(validPhone);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidPhoneNumber_WithValidPhoneWithInternationalFormat_ReturnsTrue()
    {
        // Arrange
        string validPhone = "+55 11 99999-9999";

        // Act
        bool result = _validationService.IsValidPhoneNumber(validPhone);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidPhoneNumber_WithNullPhone_ReturnsFalse()
    {
        // Arrange
        string? nullPhone = null;

        // Act
        bool result = _validationService.IsValidPhoneNumber(nullPhone!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidPhoneNumber_WithEmptyPhone_ReturnsFalse()
    {
        // Arrange
        string emptyPhone = string.Empty;

        // Act
        bool result = _validationService.IsValidPhoneNumber(emptyPhone);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidPhoneNumber_WithWhitespacePhone_ReturnsFalse()
    {
        // Arrange
        string whitespacePhone = "   ";

        // Act
        bool result = _validationService.IsValidPhoneNumber(whitespacePhone);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidPhoneNumber_WithTooFewDigits_ReturnsFalse()
    {
        // Arrange
        string invalidPhone = "1234567";

        // Act
        bool result = _validationService.IsValidPhoneNumber(invalidPhone);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidPhoneNumber_WithTooManyDigits_ReturnsFalse()
    {
        // Arrange
        string invalidPhone = "123456789012345678";

        // Act
        bool result = _validationService.IsValidPhoneNumber(invalidPhone);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidPhoneNumber_WithOnlyLetters_ReturnsFalse()
    {
        // Arrange
        string invalidPhone = "abcdefghij";

        // Act
        bool result = _validationService.IsValidPhoneNumber(invalidPhone);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("(11) 99999-9999")]
    [InlineData("11 99999-9999")]
    [InlineData("11999999999")]
    [InlineData("+55 11 99999-9999")]
    [InlineData("+5511999999999")]
    [InlineData("(21) 3333-4444")]
    [InlineData("21998765432")]
    public void IsValidPhoneNumber_WithVariousValidFormats_ReturnsTrue(string phone)
    {
        // Act
        bool result = _validationService.IsValidPhoneNumber(phone);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("123")] // Too few digits
    [InlineData("12345")] // Too few digits
    [InlineData("123456789012345678901")] // Too many digits
    [InlineData("abc-def-ghij")] // Non-numeric
    public void IsValidPhoneNumber_WithInvalidFormats_ReturnsFalse(string phone)
    {
        // Act
        bool result = _validationService.IsValidPhoneNumber(phone);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Error Message Tests

    [Fact]
    public void GetEmailValidationErrorMessage_ReturnsPortugueseMessage()
    {
        // Act
        string message = _validationService.GetEmailValidationErrorMessage();

        // Assert
        Assert.NotEmpty(message);
        Assert.Contains("Email", message);
        Assert.Contains("inválido", message);
    }

    [Fact]
    public void GetPhoneNumberValidationErrorMessage_ReturnsPortugueseMessage()
    {
        // Act
        string message = _validationService.GetPhoneNumberValidationErrorMessage();

        // Assert
        Assert.NotEmpty(message);
        Assert.Contains("Número de telefone", message);
        Assert.Contains("inválido", message);
    }

    #endregion
}
