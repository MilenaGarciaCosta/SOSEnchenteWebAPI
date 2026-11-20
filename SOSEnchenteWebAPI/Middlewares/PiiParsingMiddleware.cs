using System.Text;
using SOSEnchenteWebAPI.DbAdapter;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Services;

namespace SOSEnchenteWebAPI.Middlewares;

// Intercepta requisições JSON na entrada (source) para mapear, taguear e mascarar PII
public class PiiParsingMiddleware
{
    public const string TaintedContextKey = "TaintedDataContext";
    private readonly RequestDelegate _next;
    private readonly ILogger<PiiParsingMiddleware> _logger;

    public PiiParsingMiddleware(RequestDelegate next, ILogger<PiiParsingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IPiiScannerService piiScanner,
        AppDbContext dbContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Ignora SignalR, Swagger, arquivos estáticos e leitura do próprio painel
        if (path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/security/dashboard", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.Request.ContentLength > 0 &&
            context.Request.ContentType != null &&
            context.Request.ContentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var taintedContext = piiScanner.ScanJsonPayload(body);
            context.Items[TaintedContextKey] = taintedContext;

            if (taintedContext.HasSensitiveData)
            {
                var piiTypes = string.Join(", ", taintedContext.DetectedFields.Select(f => f.PiiType).Distinct());

                // Registra somente a prévia mascarada no log do servidor
                _logger.LogInformation(
                    "Motor de Parsing PII detectou dados sensíveis em {Path}. Tipos: {Types}. Payload mascarado: {Preview}",
                    path,
                    piiTypes,
                    taintedContext.SanitizedPayload);

                dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
                {
                    EventType = "PII_MASKED_ON_INGRESS",
                    RiskLevel = "Verde",
                    Endpoint = path,
                    TechnicalDetails = $"PII mapeado e tagueado na entrada ({piiTypes}).",
                    DetectedPiiTypes = piiTypes,
                    MaskedPreview = taintedContext.SanitizedPayload,
                    CitizenMessage = "Seus dados pessoais foram identificados e protegidos automaticamente antes do processamento."
                });

                await dbContext.SaveChangesAsync();
            }
        }

        await _next(context);
    }
}