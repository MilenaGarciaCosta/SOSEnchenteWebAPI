namespace SOSEnchenteWebAPI.Domain.DTOs;

public class HealthProfileDto
{
    public int Id { get; set; }
    public int MobilityLevel { get; set; }
    public bool LivesAlone { get; set; }
    public List<string> HealthConditions { get; set; } = new();
}
