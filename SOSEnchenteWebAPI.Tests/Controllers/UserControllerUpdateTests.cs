using System.Security.Claims;
using Xunit;
using Moq;
using SOSEnchenteWebAPI.Controllers;
using SOSEnchenteWebAPI.Domain.DTOs;
using SOSEnchenteWebAPI.Domain.Interfaces;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SOSEnchenteWebAPI.Tests.Controllers;

public class UserControllerUpdateTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IPasswordService> _mockPasswordService;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IValidationService> _mockValidationService;
    private readonly UserController _controller;

    public UserControllerUpdateTests()
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

        // Por padrão, nenhum usuário logado
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _mockValidationService
            .Setup(v => v.IsValidPhoneNumber(It.IsAny<string>()))
            .Returns(true);
    }

    private void SetAuthenticatedUser(int userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static UpdateUserRequest CreateValidRequest()
    {
        return new UpdateUserRequest
        {
            Name = "Maria Silva",
            Age = 78,
            PhoneNumber = "11900000001",
            HealthProfile = new UpdateHealthProfileRequest
            {
                MobilityLevel = 4,
                LivesAlone = true,
                HealthConditions = new List<string> { "Hipertensão" }
            }
        };
    }

    private static User CreateExistingUser()
    {
        return new User
        {
            Id = 1,
            Email = "maria@teste.com",
            Name = "Maria",
            Age = 70,
            PhoneNumber = "11900000001",
            HealthProfile = new HealthProfile
            {
                MobilityLevel = 1,
                LivesAlone = false,
                HealthConditions = new List<string>()
            }
        };
    }

    #region Authorization

    [Fact]
    public async Task UpdateUser_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        _mockUserRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task UpdateUser_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        SetAuthenticatedUser(1);
        var request = CreateValidRequest();
        request.Name = "   ";

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.False(authResponse.Success);
        Assert.Contains("nome", authResponse.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public async Task UpdateUser_WithInvalidAge_ReturnsBadRequest(int age)
    {
        // Arrange
        SetAuthenticatedUser(1);
        var request = CreateValidRequest();
        request.Age = age;

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.Contains("idade", authResponse.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task UpdateUser_WithInvalidMobilityLevel_ReturnsBadRequest(int mobilityLevel)
    {
        // Arrange
        SetAuthenticatedUser(1);
        var request = CreateValidRequest();
        request.HealthProfile!.MobilityLevel = mobilityLevel;

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.Contains("mobilidade", authResponse.Message);
    }

    [Fact]
    public async Task UpdateUser_WithInvalidPhoneNumber_ReturnsBadRequest()
    {
        // Arrange
        SetAuthenticatedUser(1);
        var request = CreateValidRequest();
        request.PhoneNumber = "123";

        _mockValidationService
            .Setup(v => v.IsValidPhoneNumber("123"))
            .Returns(false);

        _mockValidationService
            .Setup(v => v.GetPhoneNumberValidationErrorMessage())
            .Returns("Número de telefone inválido.");

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var authResponse = Assert.IsType<AuthResponse>(badRequestResult.Value);
        Assert.Contains("Número de telefone", authResponse.Message);
    }

    #endregion

    #region Update

    [Fact]
    public async Task UpdateUser_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        SetAuthenticatedUser(99);
        var request = CreateValidRequest();

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(99))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
        _mockUserRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateUser_WithValidData_UpdatesUserAndReturnsOk()
    {
        // Arrange
        SetAuthenticatedUser(1);
        var request = CreateValidRequest();
        request.Name = "  Maria Silva  ";
        var existingUser = CreateExistingUser();

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(1))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userDto = Assert.IsType<UserDto>(okResult.Value);
        Assert.Equal("Maria Silva", userDto.Name); // espaços removidos
        Assert.Equal(78, userDto.Age);
        Assert.Equal(4, userDto.HealthProfile!.MobilityLevel);
        Assert.True(userDto.HealthProfile.LivesAlone);
        Assert.Contains("Hipertensão", userDto.HealthProfile.HealthConditions);
        _mockUserRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateUser_WithoutHealthProfile_KeepsExistingHealthProfile()
    {
        // Arrange
        SetAuthenticatedUser(1);
        var request = CreateValidRequest();
        request.HealthProfile = null;
        var existingUser = CreateExistingUser();

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(1))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var userDto = Assert.IsType<UserDto>(okResult.Value);
        Assert.Equal(1, userDto.HealthProfile!.MobilityLevel); // continua como estava
        Assert.False(userDto.HealthProfile.LivesAlone);
    }

    [Fact]
    public async Task UpdateUser_WithDatabaseException_ReturnsInternalServerError()
    {
        // Arrange
        SetAuthenticatedUser(1);
        var request = CreateValidRequest();

        _mockUserRepository
            .Setup(r => r.GetUserByIdAsync(1))
            .ReturnsAsync(CreateExistingUser());

        _mockUserRepository
            .Setup(r => r.SaveChangesAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.UpdateUser(request);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        var authResponse = Assert.IsType<AuthResponse>(statusCodeResult.Value);
        Assert.Contains("Database error", authResponse.Message);
    }

    #endregion
}