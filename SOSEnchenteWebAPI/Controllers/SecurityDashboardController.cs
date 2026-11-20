using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SOSEnchenteWebAPI.DbAdapter;
using SOSEnchenteWebAPI.Middlewares;
using SOSEnchenteWebAPI.Services;

namespace SOSEnchenteWebAPI.Controllers;

public class AuditLeakSimulationRequest
{
    public string Documento { get; set; } = string.Empty;
    public string LaudoMedico { get; set; } = string.Empty;
    public string Scenario { get; set; } = "http_leak";
}

[Route("api/security")]
[ApiController]
public class SecurityDashboardController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ISafeLoggerService _safeLogger;

    public SecurityDashboardController(AppDbContext dbContext, ISafeLoggerService safeLogger)
    {
        _dbContext = dbContext;
        _safeLogger = safeLogger;
    }

    // Fornece o farol de risco, métricas e histórico traduzido pela IA para o Dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var events = await _dbContext.SecurityAuditEvents
            .OrderByDescending(e => e.TimestampUtc)
            .Take(20)
            .ToListAsync();

        var latestEvent = events.FirstOrDefault();
        var currentRiskLevel = latestEvent?.RiskLevel ?? "Verde";
        var latestCitizenMessage = latestEvent?.CitizenMessage
            ?? "Sistema operando normalmente. Nenhum vazamento de dados pessoais foi detectado.";

        var totalPiiMasked = await _dbContext.SecurityAuditEvents
            .CountAsync(e => e.EventType == "PII_MASKED_ON_INGRESS");

        var totalBlockedLeaks = await _dbContext.SecurityAuditEvents
            .CountAsync(e => e.EventType.StartsWith("TAINT_"));

        return Ok(new
        {
            riskLevel = currentRiskLevel,
            latestCitizenMessage,
            metrics = new
            {
                totalEvents = events.Count,
                totalPiiMasked,
                totalBlockedLeaks
            },
            events
        });
    }

    // Endpoint de auditoria controlada para demonstrar os bloqueios ao vivo no vídeo do painel
    [HttpPost("simulate-audit")]
    public async Task<IActionResult> SimulateAudit([FromBody] AuditLeakSimulationRequest request)
    {
        var taintedContext = HttpContext.Items[PiiParsingMiddleware.TaintedContextKey] as TaintedDataContext;

        if (request.Scenario.Equals("log_leak", StringComparison.OrdinalIgnoreCase))
        {
            var rawAttempt = $"Tentativa insegura de log: documento={request.Documento}, laudo={request.LaudoMedico}";
            var allowed = await _safeLogger.TryLogInfoAsync(rawAttempt, taintedContext, "/api/security/simulate-audit");

            return Ok(new
            {
                blocked = !allowed,
                scenario = "log_leak",
                message = "Teste de bloqueio de log concluído. Verifique o painel de risco."
            });
        }

        // Simula vazamento HTTP não autenticado para ser barrado pelo TaintSinkMiddleware
        return Ok(new
        {
            leakedDocument = request.Documento,
            leakedHealthRecord = request.LaudoMedico
        });
    }
}