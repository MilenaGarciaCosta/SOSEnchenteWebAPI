using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using SOSEnchenteWebAPI.Controllers;
using SOSEnchenteWebAPI.DbAdapter;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Middlewares;
using SOSEnchenteWebAPI.Services;
using Xunit;

namespace SOSEnchenteWebAPI.Tests.Controllers;

public class SecurityDashboardControllerTests
{
    private readonly List<SecurityAuditEvent> _auditEvents;
    private readonly Mock<AppDbContext> _mockDbContext;
    private readonly Mock<ISafeLoggerService> _mockSafeLogger;
    private readonly SecurityDashboardController _controller;

    public SecurityDashboardControllerTests()
    {
        _auditEvents = new List<SecurityAuditEvent>();
        _mockDbContext = CreateMockDbContext(_auditEvents);
        _mockSafeLogger = new Mock<ISafeLoggerService>();

        _controller = new SecurityDashboardController(_mockDbContext.Object, _mockSafeLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task GetDashboard_WhenNoEventsExist_ReturnsGreenStatusAndDefaultMessage()
    {
        // Execução
        var result = await _controller.GetDashboard();

        // Verificação
        var okResult = Assert.IsType<OkObjectResult>(result);
        using var payload = ParseJson(okResult.Value!);

        Assert.Equal("Verde", payload.RootElement.GetProperty("riskLevel").GetString());
        Assert.Contains("normalmente", payload.RootElement.GetProperty("latestCitizenMessage").GetString());
        Assert.Equal(0, payload.RootElement.GetProperty("metrics").GetProperty("totalPiiMasked").GetInt32());
        Assert.Equal(0, payload.RootElement.GetProperty("metrics").GetProperty("totalBlockedLeaks").GetInt32());
    }

    [Fact]
    public async Task GetDashboard_WithAuditEvents_ReturnsLatestRiskLevelAndMetrics()
    {
        // Preparação
        _auditEvents.Add(new SecurityAuditEvent
        {
            Id = 1,
            TimestampUtc = DateTime.UtcNow.AddMinutes(-5),
            EventType = "PII_MASKED_ON_INGRESS",
            RiskLevel = "Verde",
            Endpoint = "/api/user/register",
            DetectedPiiTypes = "CPF, HEALTH_DATA",
            MaskedPreview = """{"documento":"***.456.***-00"}""",
            CitizenMessage = "Seus dados pessoais foram protegidos automaticamente."
        });

        _auditEvents.Add(new SecurityAuditEvent
        {
            Id = 2,
            TimestampUtc = DateTime.UtcNow,
            EventType = "TAINT_HTTP_SINK_BLOCKED",
            RiskLevel = "Vermelho",
            Endpoint = "/api/security/simulate-audit",
            DetectedPiiTypes = "CPF, HEALTH_DATA",
            MaskedPreview = """{"documento":"***.456.***-00"}""",
            CitizenMessage = "Tentaram acessar seu documento de saúde, mas bloqueamos a ação para proteger sua privacidade."
        });

        // Execução
        var result = await _controller.GetDashboard();

        // Verificação
        var okResult = Assert.IsType<OkObjectResult>(result);
        using var payload = ParseJson(okResult.Value!);

        Assert.Equal("Vermelho", payload.RootElement.GetProperty("riskLevel").GetString());
        Assert.Contains("bloqueamos a ação", payload.RootElement.GetProperty("latestCitizenMessage").GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("metrics").GetProperty("totalPiiMasked").GetInt32());
        Assert.Equal(1, payload.RootElement.GetProperty("metrics").GetProperty("totalBlockedLeaks").GetInt32());
        Assert.Equal(2, payload.RootElement.GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task SimulateAudit_WithLogLeakScenario_CallsSafeLoggerAndReturnsBlockedTrue()
    {
        // Preparação
        var taintedContext = new TaintedDataContext();
        _controller.HttpContext.Items[PiiParsingMiddleware.TaintedContextKey] = taintedContext;

        var request = new AuditLeakSimulationRequest
        {
            Documento = "123.456.789-00",
            LaudoMedico = "Paciente com diabetes",
            Scenario = "log_leak"
        };

        _mockSafeLogger
            .Setup(l => l.TryLogInfoAsync(It.IsAny<string>(), taintedContext, "/api/security/simulate-audit"))
            .ReturnsAsync(false);

        // Execução
        var result = await _controller.SimulateAudit(request);

        // Verificação
        var okResult = Assert.IsType<OkObjectResult>(result);
        using var payload = ParseJson(okResult.Value!);

        Assert.True(payload.RootElement.GetProperty("blocked").GetBoolean());
        Assert.Equal("log_leak", payload.RootElement.GetProperty("scenario").GetString());
        _mockSafeLogger.Verify(
            l => l.TryLogInfoAsync(It.IsAny<string>(), taintedContext, "/api/security/simulate-audit"),
            Times.Once);
    }

    [Fact]
    public async Task SimulateAudit_WithHttpLeakScenario_ReturnsPayloadForMiddlewareInterception()
    {
        // Preparação
        var request = new AuditLeakSimulationRequest
        {
            Documento = "123.456.789-00",
            LaudoMedico = "Paciente com mobilidade reduzida",
            Scenario = "http_leak"
        };

        // Execução
        var result = await _controller.SimulateAudit(request);

        // Verificação
        var okResult = Assert.IsType<OkObjectResult>(result);
        using var payload = ParseJson(okResult.Value!);

        Assert.Equal("123.456.789-00", payload.RootElement.GetProperty("leakedDocument").GetString());
        Assert.Equal("Paciente com mobilidade reduzida", payload.RootElement.GetProperty("leakedHealthRecord").GetString());
        _mockSafeLogger.Verify(
            l => l.TryLogInfoAsync(It.IsAny<string>(), It.IsAny<TaintedDataContext?>(), It.IsAny<string>()),
            Times.Never);
    }

    private static JsonDocument ParseJson(object value)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(value));
    }

    // Cria um DbContext simulado compatível com consultas assíncronas do EF Core sem pacotes extras
    private static Mock<AppDbContext> CreateMockDbContext(List<SecurityAuditEvent> backingList)
    {
        var queryable = backingList.AsQueryable();
        var mockSet = new Mock<DbSet<SecurityAuditEvent>>();

        mockSet.As<IAsyncEnumerable<SecurityAuditEvent>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(() => new TestAsyncEnumerator<SecurityAuditEvent>(backingList.GetEnumerator()));

        mockSet.As<IQueryable<SecurityAuditEvent>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<SecurityAuditEvent>(queryable.Provider));

        mockSet.As<IQueryable<SecurityAuditEvent>>()
            .Setup(m => m.Expression)
            .Returns(queryable.Expression);

        mockSet.As<IQueryable<SecurityAuditEvent>>()
            .Setup(m => m.ElementType)
            .Returns(queryable.ElementType);

        mockSet.As<IQueryable<SecurityAuditEvent>>()
            .Setup(m => m.GetEnumerator())
            .Returns(() => backingList.GetEnumerator());

        mockSet.Setup(m => m.Add(It.IsAny<SecurityAuditEvent>()))
            .Callback<SecurityAuditEvent>(backingList.Add);

        var options = new DbContextOptions<AppDbContext>();
        var mockContext = new Mock<AppDbContext>(options);
        mockContext.Object.SecurityAuditEvents = mockSet.Object;
        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        return mockContext;
    }

    private sealed class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        public TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object? Execute(Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _inner.Execute<TResult>(expression);
        }

        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var expectedResultType = typeof(TResult).GetGenericArguments()[0];
            var executionResult = typeof(IQueryProvider)
                .GetMethod(
                    name: nameof(IQueryProvider.Execute),
                    genericParameterCount: 1,
                    types: new[] { typeof(Expression) })!
                .MakeGenericMethod(expectedResultType)
                .Invoke(_inner, new object[] { expression });

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(expectedResultType)
                .Invoke(null, new object[] { executionResult })!;
        }
    }

    private sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable)
            : base(enumerable)
        {
        }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        {
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    private sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            _inner = inner;
        }

        public T Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(_inner.MoveNext());
        }
    }
}