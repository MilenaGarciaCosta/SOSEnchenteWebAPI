namespace SOSEnchenteWebAPI.Domain.DTOs;

public class RescueSummaryDto
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Priority { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}