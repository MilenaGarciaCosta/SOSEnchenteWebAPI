using Xunit;
using Moq;
using SOSEnchenteWebAPI.Controllers;
using SOSEnchenteWebAPI.Domain.DTOs;
using SOSEnchenteWebAPI.Domain.Interfaces;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace SOSEnchenteWebAPI.Tests.Controllers;

public class AdminControllerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IPriorityService> _mockPriorityService;
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockPriorityService = new Mock<IPriorityService>();

        _controller = new AdminController(
            _mockUserRepository.Object,
            _mockPriorityService.Object
        );
    }

    private void SetupSummary(User user, int score)
    {
        _mockPriorityService
            .Setup(p => p.CalculateRescueSummary(user))
            .Returns(new RescueSummaryDto
            {
                UserId = user.Id,
                Name = user.Name,
                Age = user.Age,
                Score = score
            });
    }

    [Fact]
    public async Task GetRescueQueue_ReturnsUsersOrderedByScoreDescending()
    {
        // Arrange
        var lowPriority = new User { Id = 1, Name = "Baixa", Age = 30 };
        var highPriority = new User { Id = 2, Name = "Alta", Age = 30 };
        var mediumPriority = new User { Id = 3, Name = "Média", Age = 30 };

        _mockUserRepository
            .Setup(r => r.GetAllUsersAsync())
            .ReturnsAsync(new List<User> { lowPriority, highPriority, mediumPriority });

        SetupSummary(lowPriority, 1);
        SetupSummary(highPriority, 9);
        SetupSummary(mediumPriority, 5);

        // Act
        var result = await _controller.GetRescueQueue();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var queue = Assert.IsType<List<RescueSummaryDto>>(okResult.Value);
        Assert.Equal(new[] { 2, 3, 1 }, queue.Select(s => s.UserId));
    }

    [Fact]
    public async Task GetRescueQueue_WithSameScore_OrdersOlderFirst()
    {
        // Arrange
        var younger = new User { Id = 1, Name = "Mais novo", Age = 40 };
        var older = new User { Id = 2, Name = "Mais velho", Age = 85 };

        _mockUserRepository
            .Setup(r => r.GetAllUsersAsync())
            .ReturnsAsync(new List<User> { younger, older });

        SetupSummary(younger, 5);
        SetupSummary(older, 5);

        // Act
        var result = await _controller.GetRescueQueue();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var queue = Assert.IsType<List<RescueSummaryDto>>(okResult.Value);
        Assert.Equal(2, queue[0].UserId);
        Assert.Equal(1, queue[1].UserId);
    }

    [Fact]
    public async Task GetRescueQueue_WithNoUsers_ReturnsEmptyList()
    {
        // Arrange
        _mockUserRepository
            .Setup(r => r.GetAllUsersAsync())
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _controller.GetRescueQueue();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var queue = Assert.IsType<List<RescueSummaryDto>>(okResult.Value);
        Assert.Empty(queue);
        _mockPriorityService.Verify(p => p.CalculateRescueSummary(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task GetRescueQueue_CalculatesSummaryForEachUser()
    {
        // Arrange
        var users = new List<User>
        {
            new User { Id = 1, Name = "A", Age = 30 },
            new User { Id = 2, Name = "B", Age = 40 },
            new User { Id = 3, Name = "C", Age = 50 }
        };

        _mockUserRepository
            .Setup(r => r.GetAllUsersAsync())
            .ReturnsAsync(users);

        foreach (var user in users)
        {
            SetupSummary(user, 1);
        }

        // Act
        await _controller.GetRescueQueue();

        // Assert
        _mockPriorityService.Verify(p => p.CalculateRescueSummary(It.IsAny<User>()), Times.Exactly(3));
    }
}