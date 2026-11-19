namespace SOSEnchenteWebAPI.Domain.DTOs;

public class AlertMessage
{
    public string Type { get; set; } = string.Empty;
    public string? Level { get; set; }
    public int? WaterLevelCm { get; set; }
}

public class AlertDto
{
    public string Type { get; set; } = string.Empty;
    public string? Level { get; set; }
    public int? DistanceCm { get; set; }
    public DateTime CreatedAt { get; set; }
}