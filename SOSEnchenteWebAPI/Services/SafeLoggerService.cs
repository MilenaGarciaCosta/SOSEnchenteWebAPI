using SOSEnchenteWebAPI.DbAdapter;
using SOSEnchenteWebAPI.Domain.Models;

namespace SOSEnchenteWebAPI.Services;

public interface ISafeLoggerService
{
    Task<bool> TryLogInfoAsync(string message, TaintedDataContext? taintedContext, string endpoint);
}

public class SafeLoggerService : ISafeLoggerService
{
    private readonly ILogger<SafeLoggerService> _logger;
    private readonly IPiiScannerService _piiScanner;
    private readonly IAiRiskTranslatorService _aiTranslator;
    private readonly AppDbContext _dbContext;

    public SafeLoggerService(
        ILogger<SafeLoggerService> logger,
        IPiiScannerService piiScanner,
        IAiRiskTranslatorService aiTranslator,
        AppDbContext dbContext)
    {
        _logger = logger;
        _piiScanner = piiScanner;
        _aiTranslator = aiTranslator;
        _dbContext = dbContext;
    }

    // Impede gravação em log caso o texto contenha dado marcado como sensível (PII)
    public async Task<bool> TryLogInfoAsync(string message, TaintedDataContext? taintedContext, string endpoint)
    {
        if (_piiScanner.ContainsTaintedPlaintext(message, taintedContext))
        {
            const string technicalEvent = "Taint Sink Blocked: tentativa de gravação de PII em log de sistema em texto claro";
            var citizenMessage = await _aiTranslator.TranslateTechnicalBlockAsync(technicalEvent, endpoint);

            var piiTypes = taintedContext != null && taintedContext.HasSensitiveData
                ? string.Join(", ", taintedContext.DetectedFields.Select(f => f.PiiType).Distinct())
                : "PII_SENSIVEL";

            var maskedPreview = taintedContext != null && !string.IsNullOrWhiteSpace(taintedContext.SanitizedPayload)
                ? taintedContext.SanitizedPayload
                : "[CONTEUDO_BLOQUEADO]";

            _dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
            {
                EventType = "TAINT_LOG_SINK_BLOCKED",
                RiskLevel = "Amarelo",
                Endpoint = endpoint,
                TechnicalDetails = technicalEvent,
                DetectedPiiTypes = piiTypes,
                MaskedPreview = maskedPreview,
                CitizenMessage = citizenMessage
            });

            await _dbContext.SaveChangesAsync();

            _logger.LogWarning("Bloqueio de Taint Analysis no sink de log em {Endpoint}. Vazamento evitado.", endpoint);
            return false;
        }

        _logger.LogInformation("Log seguro registrado em {Endpoint}: {Message}", endpoint, message);
        return true;
    }
}