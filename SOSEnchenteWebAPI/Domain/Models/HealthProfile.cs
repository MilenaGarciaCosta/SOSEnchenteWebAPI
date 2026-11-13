namespace SOSEnchenteWebAPI.Domain.Models;
public class HealthProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int MobilityLevel { get; set; }
    public bool LivesAlone { get; set; }
    public List<string> HealthConditions { get; set; } = new List<string>();
}
