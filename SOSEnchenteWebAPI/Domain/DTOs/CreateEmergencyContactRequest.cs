namespace SOSEnchenteWebAPI.Domain.DTOs;

public class CreateEmergencyContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
