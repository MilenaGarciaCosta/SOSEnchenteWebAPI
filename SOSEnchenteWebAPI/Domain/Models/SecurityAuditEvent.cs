namespace SOSEnchenteWebAPI.Domain.Models;
public class SecurityAuditEvent
{
    public int Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "Verde";
    public string Endpoint { get; set; } = string.Empty;
    public string TechnicalDetails { get; set; } = string.Empty;
    public string DetectedPiiTypes { get; set; } = string.Empty;
    public string MaskedPreview { get; set; } = string.Empty;
    public string CitizenMessage { get; set; } = string.Empty;
}