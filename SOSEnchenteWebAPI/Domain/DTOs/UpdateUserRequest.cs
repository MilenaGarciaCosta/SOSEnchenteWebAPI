namespace SOSEnchenteWebAPI.Domain.DTOs;

public class UpdateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public UpdateHealthProfileRequest? HealthProfile { get; set; }
}

public class UpdateHealthProfileRequest
{
    public int MobilityLevel { get; set; }
    public bool LivesAlone { get; set; }
    public List<string> HealthConditions { get; set; } = new();
}