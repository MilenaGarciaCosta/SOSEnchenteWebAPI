namespace SOSEnchenteWebAPI.Domain.Models;

public class Dependent
{
    public int UserId { get; set; }
    public int DependentId { get; set; }
    public User User { get; set; } = null!;
    public User DependentUser { get; set; } = null!;
}
