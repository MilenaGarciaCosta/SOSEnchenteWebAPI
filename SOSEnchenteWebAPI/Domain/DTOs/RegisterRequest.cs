namespace SOSEnchenteWebAPI.Domain.DTOs;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public HealthProfileRequest HealthProfile { get; set; } = new();
}

public class HealthProfileRequest
{
    public int MobilityLevel { get; set; }
    public bool LivesAlone { get; set; }
    public List<string> HealthConditions { get; set; } = new();
}
