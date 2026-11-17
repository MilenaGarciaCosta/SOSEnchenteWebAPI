using SOSEnchenteWebAPI.Domain.DTOs;
using SOSEnchenteWebAPI.Domain.Models;

namespace SOSEnchenteWebAPI.Services;

public interface IPriorityService
{
    RescueSummaryDto CalculateRescueSummary(User user);
}

public class PriorityService : IPriorityService
{
    private const int HighPriorityMinScore = 7;
    private const int MediumPriorityMinScore = 4;
    private const int MaxConditionPoints = 3;

    public RescueSummaryDto CalculateRescueSummary(User user)
    {
        var score = 0;
        var tags = new List<string>();
        var health = user.HealthProfile;

        if (health != null)
        {
            var (mobilityPoints, mobilityTag) = health.MobilityLevel switch
            {
                2 => (1, "Dificuldade leve"),
                3 => (3, "Precisa de apoio"),
                4 => (5, "Mobilidade muito reduzida"),
                _ => (0, (string?)null)
            };
            score += mobilityPoints;
            if (mobilityTag != null) tags.Add(mobilityTag);

            if (health.LivesAlone)
            {
                score += 2;
                tags.Add("Mora sozinho(a)");
            }

            score += Math.Min(health.HealthConditions.Count, MaxConditionPoints);
            tags.AddRange(health.HealthConditions);
        }

        if (user.Age >= 80)
        {
            score += 2;
            tags.Add("Idoso(a) 80+");
        }
        else if (user.Age >= 60)
        {
            score += 1;
            tags.Add("Idoso(a) 60+");
        }

        return new RescueSummaryDto
        {
            UserId = user.Id,
            Name = user.Name,
            Age = user.Age,
            PhoneNumber = user.PhoneNumber,
            Score = score,
            Priority = GetPriorityLabel(score),
            Tags = tags
        };
    }

    private static string GetPriorityLabel(int score)
    {
        if (score >= HighPriorityMinScore) return "Alta";
        if (score >= MediumPriorityMinScore) return "Média";
        return "Baixa";
    }
}