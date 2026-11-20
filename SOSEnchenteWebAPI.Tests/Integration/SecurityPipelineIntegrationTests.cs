using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SOSEnchenteWebAPI.DbAdapter;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Middlewares;
using SOSEnchenteWebAPI.Services;
using Xunit;

namespace SOSEnchenteWebAPI.Tests.Integration;

public class SecurityPipelineIntegrationTests
{
    private readonly PiiScannerService _piiScanner;
    private readonly Mock<IAiRiskTranslatorService> _mockAiTranslator;
    private readonly List<SecurityAuditEvent> _auditEvents;
    private readonly Mock<AppDbContext> _mockDbContext;

    public SecurityPipelineIntegrationTests()
    {
        _piiScanner = new PiiScannerService();
        _mockAiTranslator = new Mock<IAiRiskTranslatorService>();
        _auditEvents = new List<SecurityAuditEvent>();
        _mockDbContext = CreateMockDbContext(_auditEvents);
    }

    [Fact]
    public async Task Pipeline_UnauthenticatedResponseLeakingPii_BlocksWith403AndAuditsEvents()
    {
        // Preparação
        _mockAiTranslator
            .Setup(x => x.TranslateTechnicalBlockAsync(It.IsAny<string>(), "/api/security/simulate-audit"))
            .ReturnsAsync("Tentaram expor seu documento sem autorização, mas bloqueamos a ação para proteger você.");

        RequestDelegate unsafeEndpoint = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            await ctx.Response.WriteAsync("""{"leakedDocument":"123.456.789-00"}""");
        };

        var taintMiddleware = new TaintSinkMiddleware(unsafeEndpoint, Mock.Of<ILogger<TaintSinkMiddleware>>());
        var parsingMiddleware = new PiiParsingMiddleware(
            ctx => taintMiddleware.InvokeAsync(ctx, _piiScanner, _mockAiTranslator.Object, _mockDbContext.Object),
            Mock.Of<ILogger<PiiParsingMiddleware>>());

        var httpContext = CreateJsonHttpContext(
            "/api/security/simulate-audit",
            """{"documento":"123.456.789-00","laudoMedico":"Paciente com diabetes"}""",
            isAuthenticated: false);

        // Execução
        await parsingMiddleware.InvokeAsync(httpContext, _piiScanner, _mockDbContext.Object);

        // Verificação
        var responseBody = await ReadResponseBodyAsync(httpContext);
        using var responseJson = JsonDocument.Parse(responseBody);
        var citizenMessage = responseJson.RootElement.GetProperty("citizenMessage").GetString();

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.DoesNotContain("123.456.789-00", responseBody);
        Assert.Contains("bloqueamos a ação", citizenMessage);
        Assert.Contains(_auditEvents, e => e.EventType == "PII_MASKED_ON_INGRESS" && e.MaskedPreview.Contains("***.456.***-00"));
        Assert.Contains(_auditEvents, e => e.EventType == "TAINT_HTTP_SINK_BLOCKED" && e.RiskLevel == "Vermelho");
    }

    [Fact]
    public async Task Pipeline_UnhandledException_PreventsTechnicalLeakAndReturnsSafe500()
    {
        // Preparação
        _mockAiTranslator
            .Setup(x => x.TranslateTechnicalBlockAsync(It.IsAny<string>(), "/api/user/update"))
            .ReturnsAsync("Ocorreu uma falha interna, mas seus dados pessoais permaneceram protegidos.");

        RequestDelegate failingEndpoint = _ =>
            throw new InvalidOperationException("Falha SQL ao gravar CPF 123.456.789-00 na tabela Users");

        var taintMiddleware = new TaintSinkMiddleware(failingEndpoint, Mock.Of<ILogger<TaintSinkMiddleware>>());
        var parsingMiddleware = new PiiParsingMiddleware(
            ctx => taintMiddleware.InvokeAsync(ctx, _piiScanner, _mockAiTranslator.Object, _mockDbContext.Object),
            Mock.Of<ILogger<PiiParsingMiddleware>>());

        var httpContext = CreateJsonHttpContext(
            "/api/user/update",
            """{"documento":"123.456.789-00"}""",
            isAuthenticated: true);

        // Execução
        await parsingMiddleware.InvokeAsync(httpContext, _piiScanner, _mockDbContext.Object);

        // Verificação
        var responseBody = await ReadResponseBodyAsync(httpContext);

        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.DoesNotContain("Falha SQL", responseBody);
        Assert.DoesNotContain("123.456.789-00", responseBody);
        Assert.Contains("permaneceram protegidos", responseBody);
        Assert.Contains(_auditEvents, e => e.EventType == "TAINT_EXCEPTION_BLOCKED" && e.RiskLevel == "Vermelho");
    }

    [Fact]
    public async Task Pipeline_InternalServerErrorFromController_ReplacesTechnicalMessageWithCitizenMessage()
    {
        // Preparação
        _mockAiTranslator
            .Setup(x => x.TranslateTechnicalBlockAsync(It.IsAny<string>(), "/api/user/register"))
            .ReturnsAsync("Tivemos um problema técnico temporário, mas seus dados seguem protegidos.");

        RequestDelegate controllerReturning500 = async ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsync("""{"success":false,"message":"Database error: constraint failed"}""");
        };

        var taintMiddleware = new TaintSinkMiddleware(controllerReturning500, Mock.Of<ILogger<TaintSinkMiddleware>>());
        var parsingMiddleware = new PiiParsingMiddleware(
            ctx => taintMiddleware.InvokeAsync(ctx, _piiScanner, _mockAiTranslator.Object, _mockDbContext.Object),
            Mock.Of<ILogger<PiiParsingMiddleware>>());

        var httpContext = CreateJsonHttpContext(
            "/api/user/register",
            """{"email":"idoso@exemplo.com","password":"Senha123"}""",
            isAuthenticated: false);

        // Execução
        await parsingMiddleware.InvokeAsync(httpContext, _piiScanner, _mockDbContext.Object);

        // Verificação
        var responseBody = await ReadResponseBodyAsync(httpContext);

        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.DoesNotContain("Database error", responseBody);
        Assert.Contains("seus dados seguem protegidos", responseBody);
        Assert.Contains(_auditEvents, e => e.EventType == "TAINT_ERROR_500_BLOCKED" && e.RiskLevel == "Vermelho");
    }

    [Fact]
    public async Task Pipeline_SignalRHubPath_BypassesSecurityMiddlewaresWithoutBuffering()
    {
        // Preparação
        var hubReached = false;
        RequestDelegate hubEndpoint = async ctx =>
        {
            hubReached = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            await ctx.Response.WriteAsync("signalr-negotiate-ok");
        };

        var taintMiddleware = new TaintSinkMiddleware(hubEndpoint, Mock.Of<ILogger<TaintSinkMiddleware>>());
        var parsingMiddleware = new PiiParsingMiddleware(
            ctx => taintMiddleware.InvokeAsync(ctx, _piiScanner, _mockAiTranslator.Object, _mockDbContext.Object),
            Mock.Of<ILogger<PiiParsingMiddleware>>());

        var httpContext = CreateJsonHttpContext("/hubs/alerts/negotiate", """{"protocol":"json"}""", isAuthenticated: false);

        // Execução
        await parsingMiddleware.InvokeAsync(httpContext, _piiScanner, _mockDbContext.Object);

        // Verificação
        Assert.True(hubReached);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Empty(_auditEvents);
    }

    [Fact]
    public async Task SafeLoggerService_WhenMqttOrApiLogContainsTaintedPii_BlocksLogAndRecordsYellowEvent()
    {
        // Preparação
        _mockAiTranslator
            .Setup(x => x.TranslateTechnicalBlockAsync(It.IsAny<string>(), "mqtt://alerts"))
            .ReturnsAsync("Bloqueamos a gravação dos seus dados pessoais no registro interno do sistema.");

        var safeLogger = new SafeLoggerService(
            Mock.Of<ILogger<SafeLoggerService>>(),
            _piiScanner,
            _mockAiTranslator.Object,
            _mockDbContext.Object);

        var rawMqttPayload = "payload mqtt inválido contendo 123.456.789-00";
        var taintedContext = _piiScanner.ScanJsonPayload(rawMqttPayload);

        // Execução
        var allowed = await safeLogger.TryLogInfoAsync(
            $"Mensagem MQTT inválida recebida: {rawMqttPayload}",
            taintedContext,
            "mqtt://alerts");

        // Verificação
        Assert.False(allowed);
        Assert.Contains(_auditEvents, e => e.EventType == "TAINT_LOG_SINK_BLOCKED" && e.RiskLevel == "Amarelo");
    }

    [Fact]
    public async Task SafeLoggerService_WhenMessageIsSanitized_AllowsLogWithoutBlocking()
    {
        // Preparação
        var safeLogger = new SafeLoggerService(
            Mock.Of<ILogger<SafeLoggerService>>(),
            _piiScanner,
            _mockAiTranslator.Object,
            _mockDbContext.Object);

        var taintedContext = _piiScanner.ScanJsonPayload("""{"documento":"123.456.789-00"}""");

        // Execução
        var allowed = await safeLogger.TryLogInfoAsync(
            "Cadastro processado com documento ***.456.***-00",
            taintedContext,
            "/api/user/register");

        // Verificação
        Assert.True(allowed);
        Assert.Empty(_auditEvents);
        _mockAiTranslator.Verify(
            x => x.TranslateTechnicalBlockAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task AiRiskTranslatorService_WithValidGeminiResponse_SendsDynamicPromptAndReturnsTranslatedText()
    {
        // Preparação
        var geminiResponseJson = """
        {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": "Tentaram acessar seu documento de saúde, mas nosso sistema bloqueou a ação para proteger sua privacidade."
                            }
                        ]
                    }
                }
            ]
        }
        """;

        var stubHandler = new StubHttpMessageHandler(HttpStatusCode.OK, geminiResponseJson);
        var httpClient = new HttpClient(stubHandler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "fake-test-key",
                ["Gemini:Model"] = "gemini-2.5-flash"
            })
            .Build();

        var translator = new AiRiskTranslatorService(
            httpClient,
            configuration,
            Mock.Of<ILogger<AiRiskTranslatorService>>());

        // Execução
        var message = await translator.TranslateTechnicalBlockAsync(
            "Vazamento de PII bloqueado no módulo de saúde",
            "/api/user");

        // Verificação
        using var requestJson = JsonDocument.Parse(stubHandler.LastRequestBody);
        var sentPrompt = requestJson.RootElement
            .GetProperty("contents")[0]
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        Assert.Equal(
            "Tentaram acessar seu documento de saúde, mas nosso sistema bloqueou a ação para proteger sua privacidade.",
            message);
        Assert.Contains("Vazamento de PII bloqueado no módulo de saúde", sentPrompt);
        Assert.Contains("/api/user", sentPrompt);
        Assert.Contains("60 anos", sentPrompt);
    }

    [Fact]
    public async Task AiRiskTranslatorService_WithoutApiKey_ReturnsAccessibleFallbackMessage()
    {
        // Preparação
        var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = ""
            })
            .Build();

        var translator = new AiRiskTranslatorService(
            httpClient,
            configuration,
            Mock.Of<ILogger<AiRiskTranslatorService>>());

        // Execução
        var message = await translator.TranslateTechnicalBlockAsync("Taint Sink Blocked", "/api/user");

        // Verificação
        Assert.Contains("bloqueou a ação automaticamente", message);
    }

    private static Mock<AppDbContext> CreateMockDbContext(List<SecurityAuditEvent> backingList)
    {
        var mockSet = new Mock<DbSet<SecurityAuditEvent>>();
        mockSet.Setup(s => s.Add(It.IsAny<SecurityAuditEvent>()))
            .Callback<SecurityAuditEvent>(backingList.Add);

        var options = new DbContextOptions<AppDbContext>();
        var mockContext = new Mock<AppDbContext>(options);
        mockContext.Object.SecurityAuditEvents = mockSet.Object;
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        return mockContext;
    }

    private static DefaultHttpContext CreateJsonHttpContext(string path, string jsonBody, bool isAuthenticated)
    {
        var context = new DefaultHttpContext();
        var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

        context.Request.Path = path;
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bodyBytes.Length;
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Response.Body = new MemoryStream();

        if (isAuthenticated)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "1") }, "TestAuth");
            context.User = new ClaimsPrincipal(identity);
        }

        return context;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    // Intercepta requisições HTTP de saída para validar o prompt enviado ao Gemini sem depender de rede
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public string LastRequestBody { get; private set; } = string.Empty;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
        }
    }
}