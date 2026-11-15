using Xunit;
using Moq;
using SOSEnchenteWebAPI.Controllers;
using SOSEnchenteWebAPI.Domain.DTOs;
using SOSEnchenteWebAPI.Domain.Interfaces;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace SOSEnchenteWebAPI.Tests.Controllers;

public class UserControllerRegistrationTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IPasswordService> _mockPasswordService;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IValidationService> _mockValidationService;
    private readonly UserController _controller;

    public UserControllerRegistrationTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockPasswordService = new Mock<IPasswordService>();
        _mockJwtService = new Mock<IJwtService>();
        _mockValidationService = new Mock<IValidationService>();

        _controller = new UserController(
            _mockUserRepository.Object,
            _mockPasswordService.Object,
            _mockJwtService.Object,
            _mockValidationService.Object
        );
    }

    [Fact]
    public async Task Register_WithMissingEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = string.Empty,
            Password = "password123",
            Name = "Test User",
            Age = 30,
            PhoneNumber = "(11) 99999-9999",
            HealthProfile = new HealthProfileRequest()
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.False(authResponse.Success);
        Assert.Contains("Email", authResponse.Message);
    }

    [Fact]
    public async Task Register_WithMissingPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = string.Empty,
            Name = "Test User",
            Age = 30,
            PhoneNumber = "(11) 99999-9999",
            HealthProfile = new HealthProfileRequest()
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.False(authResponse.Success);
    }

    [Fact]
    public async Task Register_WithMissingName_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "password123",
            Name = string.Empty,
            Age = 30,
            PhoneNumber = "(11) 99999-9999",
            HealthProfile = new HealthProfileRequest()
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.False(authResponse.Success);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "invalid-email",
            Password = "password123",
            Name = "Test User",
            Age = 30,
            PhoneNumber = "(11) 99999-9999",
            HealthProfile = new HealthProfileRequest()
        };

        _mockValidationService
            .Setup(v => v.IsValidEmail(request.Email))
            .Returns(false);

        _mockValidationService
            .Setup(v => v.GetEmailValidationErrorMessage())
            .Returns("Email inválido.");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.False(authResponse.Success);
        Assert.Contains("Email", authResponse.Message);
        _mockValidationService.Verify(v => v.IsValidEmail(request.Email), Times.Once);
    }

    [Fact]
    public async Task Register_WithInvalidPhoneNumber_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "password123",
            Name = "Test User",
            Age = 30,
            PhoneNumber = "123", // Invalid phone
            HealthProfile = new HealthProfileRequest()
        };

        _mockValidationService
            .Setup(v => v.IsValidEmail(request.Email))
            .Returns(true);

        _mockValidationService
            .Setup(v => v.IsValidPhoneNumber(request.PhoneNumber))
            .Returns(false);

        _mockValidationService
            .Setup(v => v.GetPhoneNumberValidationErrorMessage())
            .Returns("Número de telefone inválido.");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.False(authResponse.Success);
        Assert.Contains("Número de telefone", authResponse.Message);
        _mockValidationService.Verify(v => v.IsValidPhoneNumber(request.PhoneNumber), Times.Once);
    }

    [Fact]
    public async Task Register_WithValidPhoneNumber_PassesValidation()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "password123",
            Name = "Test User",
            Age = 30,
            PhoneNumber = "(11) 99999-9999",
            HealthProfile = new HealthProfileRequest()
        };

        _mockValidationService
            .Setup(v => v.IsValidEmail(request.Email))
            .Returns(true);

        _mockValidationService
            .Setup(v => v.IsValidPhoneNumber(request.PhoneNumber))
            .Returns(true);

        _mockUserRepository
            .Setup(r => r.UserExistsAsync(request.Email))
            .ReturnsAsync(false);

        _mockPasswordService
            .Setup(p => p.HashPassword(request.Password))
            .Returns("hashed_password");

        var createdUser = new User
        {
            Id = 1,
            Email = request.Email,
            Password = "hashed_password",
            Name = request.Name,
            Age = request.Age,
            PhoneNumber = request.PhoneNumber,
            HealthProfile = new HealthProfile()
        };

        _mockUserRepository
            .Setup(r => r.CreateUserAsync(It.IsAny<User>()))
            .ReturnsAsync(createdUser);

        _mockJwtService
            .Setup(j => j.GenerateToken(createdUser))
            .Returns("mock_token");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(createdAtActionResult.Value);
        Assert.True(authResponse.Success);
        Assert.Equal("mock_token", authResponse.Token);
        _mockValidationService.Verify(v => v.IsValidPhoneNumber(request.PhoneNumber), Times.Once);
    }

    [Fact]
    public async Task Register_WithEmptyPhoneNumber_SkipsPhoneValidation()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "password123",
            Name = "Test User",
            Age = 30,
            PhoneNumber = string.Empty, // Empty phone
            HealthProfile = new HealthProfileRequest()
        };

        _mockValidationService
            .Setup(v => v.IsValidEmail(request.Email))
            .Returns(true);

        _mockUserRepository
            .Setup(r => r.UserExistsAsync(request.Email))
            .ReturnsAsync(false);

        _mockPasswordService
            .Setup(p => p.HashPassword(request.Password))
            .Returns("hashed_password");

        var createdUser = new User
        {
            Id = 1,
            Email = request.Email,
            Password = "hashed_password",
            Name = request.Name,
            Age = request.Age,
            PhoneNumber = request.PhoneNumber,
            HealthProfile = new HealthProfile()
        };

        _mockUserRepository
            .Setup(r => r.CreateUserAsync(It.IsAny<User>()))
            .ReturnsAsync(createdUser);

        _mockJwtService
            .Setup(j => j.GenerateToken(createdUser))
            .Returns("mock_token");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(createdAtActionResult.Value);
        Assert.True(authResponse.Success);
        // Phone validation should NOT be called for empty phone
        _mockValidationService.Verify(v => v.IsValidPhoneNumber(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_WithExistingUserEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "existing@example.com",
            Password = "password123",
            Name = "Test User",
            Age = 30,
            PhoneNumber = "(11) 99999-9999",
            HealthProfile = new HealthProfileRequest()
        };

        _mockValidationService
            .Setup(v => v.IsValidEmail(request.Email))
            .Returns(true);

        _mockValidationService
            .Setup(v => v.IsValidPhoneNumber(request.PhoneNumber))
            .Returns(true);

        _mockUserRepository
            .Setup(r => r.UserExistsAsync(request.Email))
            .ReturnsAsync(true); // User already exists

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.False(authResponse.Success);
        Assert.Contains("Email já registrado", authResponse.Message);
    }

    [Fact]
    public async Task Register_WithDatabaseException_ReturnsInternalServerError()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "password123",
            Name = "Test User",
            Age = 30,
            PhoneNumber = "(11) 99999-9999",
            HealthProfile = new HealthProfileRequest()
        };

        _mockValidationService
            .Setup(v => v.IsValidEmail(request.Email))
            .Returns(true);

        _mockValidationService
            .Setup(v => v.IsValidPhoneNumber(request.PhoneNumber))
            .Returns(true);

        _mockUserRepository
            .Setup(r => r.UserExistsAsync(request.Email))
            .ReturnsAsync(false);

        _mockPasswordService
            .Setup(p => p.HashPassword(request.Password))
            .Returns("hashed_password");

        _mockUserRepository
            .Setup(r => r.CreateUserAsync(It.IsAny<User>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.Register(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        var authResponse = Assert.IsType<AuthResponse>(statusCodeResult.Value);
        Assert.False(authResponse.Success);
        Assert.Contains("Database error", authResponse.Message);
    }
}
