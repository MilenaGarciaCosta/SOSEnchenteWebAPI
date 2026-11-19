using Xunit;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Services;

namespace SOSEnchenteWebAPI.Tests.Services;

public class PriorityServiceTests
{
    private readonly PriorityService _priorityService;

    public PriorityServiceTests()
    {
        _priorityService = new PriorityService();
    }

    private static User CreateUser(
        int age,
        int mobilityLevel = 1,
        bool livesAlone = false,
        List<string>? healthConditions = null,
        bool withHealthProfile = true)
    {
        return new User
        {
            Id = 1,
            Name = "Test User",
            Age = age,
            PhoneNumber = "11999999999",
            HealthProfile = withHealthProfile
                ? new HealthProfile
                {
                    MobilityLevel = mobilityLevel,
                    LivesAlone = livesAlone,
                    HealthConditions = healthConditions ?? new List<string>()
                }
                : null
        };
    }

    #region Priority Scenarios

    [Fact]
    public void CalculateRescueSummary_WithBedriddenElderlyLivingAlone_ReturnsHighPriority()
    {
        // Arrange
        var user = CreateUser(age: 85, mobilityLevel: 4, livesAlone: true,
            healthConditions: new List<string> { "Doença cardíaca" });

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(10, result.Score); // 5 (mobilidade) + 2 (sozinho) + 1 (condição) + 2 (80+)
        Assert.Equal("Alta", result.Priority);
        Assert.Contains("Mobilidade muito reduzida", result.Tags);
        Assert.Contains("Mora sozinho(a)", result.Tags);
        Assert.Contains("Idoso(a) 80+", result.Tags);
        Assert.Contains("Doença cardíaca", result.Tags);
    }

    [Fact]
    public void CalculateRescueSummary_WithYoungPersonWithoutDifficulty_ReturnsLowPriority()
    {
        // Arrange
        var user = CreateUser(age: 25);

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(0, result.Score);
        Assert.Equal("Baixa", result.Priority);
        Assert.Empty(result.Tags);
    }

    [Fact]
    public void CalculateRescueSummary_WithNullHealthProfile_UsesOnlyAge()
    {
        // Arrange
        var user = CreateUser(age: 70, withHealthProfile: false);

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(1, result.Score);
        Assert.Equal("Baixa", result.Priority);
        Assert.Single(result.Tags);
        Assert.Contains("Idoso(a) 60+", result.Tags);
    }

    [Fact]
    public void CalculateRescueSummary_WithManyHealthConditions_LimitsConditionPointsToThree()
    {
        // Arrange
        var conditions = new List<string> { "Diabetes", "Hipertensão", "Asma", "Gestante", "Deficiência visual" };
        var user = CreateUser(age: 30, healthConditions: conditions);

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(3, result.Score); // 5 condições, mas no máximo 3 pontos
        Assert.Equal(5, result.Tags.Count); // todas as condições aparecem como tag
    }

    [Fact]
    public void CalculateRescueSummary_CopiesBasicUserData()
    {
        // Arrange
        var user = CreateUser(age: 40);

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.Name, result.Name);
        Assert.Equal(user.Age, result.Age);
        Assert.Equal(user.PhoneNumber, result.PhoneNumber);
    }

    #endregion

    #region Score Rules

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 5)]
    public void CalculateRescueSummary_WithMobilityLevel_AddsExpectedPoints(int mobilityLevel, int expectedScore)
    {
        // Arrange
        var user = CreateUser(age: 30, mobilityLevel: mobilityLevel);

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(expectedScore, result.Score);
    }

    [Theory]
    [InlineData(59, 0)]
    [InlineData(60, 1)]
    [InlineData(79, 1)]
    [InlineData(80, 2)]
    public void CalculateRescueSummary_WithAgeBoundaries_AddsExpectedPoints(int age, int expectedScore)
    {
        // Arrange
        var user = CreateUser(age: age);

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(expectedScore, result.Score);
    }

    [Fact]
    public void CalculateRescueSummary_WhenLivingAlone_AddsTwoPoints()
    {
        // Arrange
        var user = CreateUser(age: 30, livesAlone: true);

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(2, result.Score);
        Assert.Contains("Mora sozinho(a)", result.Tags);
    }

    #endregion

    #region Priority Boundaries

    [Theory]
    [InlineData(4, true, 30, 7, "Alta")]   // 5 + 2 = 7 → limite mínimo de Alta
    [InlineData(4, false, 60, 6, "Média")] // 5 + 1 = 6 → limite máximo de Média
    [InlineData(3, false, 60, 4, "Média")] // 3 + 1 = 4 → limite mínimo de Média
    [InlineData(3, false, 30, 3, "Baixa")] // 3     → limite máximo de Baixa
    public void CalculateRescueSummary_AtPriorityBoundaries_ReturnsExpectedPriority(
        int mobilityLevel, bool livesAlone, int age, int expectedScore, string expectedPriority)
    {
        // Arrange
        var user = CreateUser(age: age, mobilityLevel: mobilityLevel, livesAlone: livesAlone);

        // Act
        var result = _priorityService.CalculateRescueSummary(user);

        // Assert
        Assert.Equal(expectedScore, result.Score);
        Assert.Equal(expectedPriority, result.Priority);
    }

    #endregion
}