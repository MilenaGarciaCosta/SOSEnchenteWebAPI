using System.Text;
using System.Text.Json;
using SOSEnchenteWebAPI.DbAdapter;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Services;

namespace SOSEnchenteWebAPI.Middlewares;

// Camada de Taint Analysis na saída (sink) que bloqueia vazamento de PII e erros técnicos
public class TaintSinkMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TaintSinkMiddleware> _logger;

    private static readonly string[] PublicAuthEndpoints =
    {
        "/api/user/login",
        "/api/user/register"
    };

    public TaintSinkMiddleware(RequestDelegate next, ILogger<TaintSinkMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IPiiScannerService piiScanner,
        IAiRiskTranslatorService aiTranslator,
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

        var originalBodyStream = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);

            responseBuffer.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(responseBuffer, Encoding.UTF8).ReadToEndAsync();

            var taintedContext = context.Items[PiiParsingMiddleware.TaintedContextKey] as TaintedDataContext;
            var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
            var isPublicAuthRoute = PublicAuthEndpoints.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase));
            var isErrorStatus = context.Response.StatusCode >= 400;
            var isInternalServerError = context.Response.StatusCode >= 500;

            var hasTaintedLeak = piiScanner.ContainsTaintedPlaintext(responseText, taintedContext);

            // Bloqueia vazamento de PII em erro ou rota não autenticada, e traduz erros 500 para o cidadão
            var shouldBlockSink = isInternalServerError || (hasTaintedLeak && (isErrorStatus || (!isAuthenticated && !isPublicAuthRoute)));

            if (shouldBlockSink)
            {
                var statusCode = isInternalServerError
                    ? StatusCodes.Status500InternalServerError
                    : StatusCodes.Status403Forbidden;

                var eventType = isInternalServerError
                    ? "TAINT_ERROR_500_BLOCKED"
                    : "TAINT_HTTP_SINK_BLOCKED";

                var technicalReason = isInternalServerError
                    ? $"Taint Sink Blocked: falha interna HTTP {context.Response.StatusCode} interceptada para evitar exposição técnica"
                    : isErrorStatus
                        ? $"Taint Sink Blocked: tentativa de vazamento de PII em resposta de erro HTTP {context.Response.StatusCode}"
                        : "Taint Sink Blocked: tentativa de exposição de PII em resposta HTTP não autenticada";

                await WriteBlockedResponseAsync(
                    context,
                    originalBodyStream,
                    statusCode,
                    eventType,
                    "Vermelho",
                    path,
                    technicalReason,
                    taintedContext,
                    aiTranslator,
                    dbContext);
                return;
            }

            responseBuffer.Seek(0, SeekOrigin.Begin);
            await responseBuffer.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção interceptada pelo TaintSinkMiddleware em {Path}.", path);

            var taintedContext = context.Items[PiiParsingMiddleware.TaintedContextKey] as TaintedDataContext;
            var technicalReason = $"Taint Sink Blocked: exceção interna bloqueada para evitar vazamento técnico em {path}";

            await WriteBlockedResponseAsync(
                context,
                originalBodyStream,
                StatusCodes.Status500InternalServerError,
                "TAINT_EXCEPTION_BLOCKED",
                "Vermelho",
                path,
                technicalReason,
                taintedContext,
                aiTranslator,
                dbContext);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static async Task WriteBlockedResponseAsync(
        HttpContext context,
        Stream originalBodyStream,
        int statusCode,
        string eventType,
        string riskLevel,
        string path,
        string technicalReason,
        TaintedDataContext? taintedContext,
        IAiRiskTranslatorService aiTranslator,
        AppDbContext dbContext)
    {
        var citizenMessage = await aiTranslator.TranslateTechnicalBlockAsync(technicalReason, path);
        var piiTypes = taintedContext != null && taintedContext.HasSensitiveData
            ? string.Join(", ", taintedContext.DetectedFields.Select(f => f.PiiType).Distinct())
            : "PII_SENSIVEL";

        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            EventType = eventType,
            RiskLevel = riskLevel,
            Endpoint = path,
            TechnicalDetails = technicalReason,
            DetectedPiiTypes = piiTypes,
            MaskedPreview = taintedContext?.SanitizedPayload ?? "[RESPOSTA_BLOQUEADA]",
            CitizenMessage = citizenMessage
        });

        await dbContext.SaveChangesAsync();

        context.Response.Body = originalBodyStream;
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var safePayload = JsonSerializer.Serialize(new
        {
            blocked = true,
            riskLevel,
            citizenMessage
        });

        await context.Response.WriteAsync(safePayload);
    }
}