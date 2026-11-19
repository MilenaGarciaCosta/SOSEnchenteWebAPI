namespace SOSEnchenteWebAPI.Domain.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public HealthProfile HealthProfile { get; set; } = new HealthProfile();
    public List<EmergencyContact> EmergencyContacts { get; set; } = new List<EmergencyContact>();
    public List<Dependent> Dependents { get; set; } = new List<Dependent>();
    public bool IsAdmin { get; set; } = false;
}
