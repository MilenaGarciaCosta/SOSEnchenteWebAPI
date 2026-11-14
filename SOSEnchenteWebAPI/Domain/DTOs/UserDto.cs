namespace SOSEnchenteWebAPI.Domain.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public HealthProfileDto? HealthProfile { get; set; }
}
