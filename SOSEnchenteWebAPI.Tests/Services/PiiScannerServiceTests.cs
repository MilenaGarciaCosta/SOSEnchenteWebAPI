using SOSEnchenteWebAPI.Services;
using Xunit;

namespace SOSEnchenteWebAPI.Tests.Services;

public class PiiScannerServiceTests
{
    private readonly PiiScannerService _service;

    public PiiScannerServiceTests()
    {
        _service = new PiiScannerService();
    }

    [Fact]
    public void ScanJsonPayload_WithSensitiveFields_DetectsAndMasksAllPii()
    {
        // Preparação
        var json = """
        {
            "documento": "123.456.789-00",
            "rg": "12.345.678-9",
            "email": "idoso.silva@exemplo.com",
            "phoneNumber": "(11) 98765-4321",
            "password": "MinhaSenhaSecreta123",
            "healthConditions": ["Diabetes tipo 2", "Mobilidade reduzida"]
        }
        """;

        // Execução
        var result = _service.ScanJsonPayload(json);

        // Verificação
        Assert.True(result.HasSensitiveData);
        Assert.Contains(result.DetectedFields, f => f.PiiType == "CPF" && f.MaskedValue == "***.456.***-00");
        Assert.Contains(result.DetectedFields, f => f.PiiType == "RG" && f.MaskedValue == "**.***.***-*");
        Assert.Contains(result.DetectedFields, f => f.PiiType == "PASSWORD" && f.MaskedValue == "********");
        Assert.Contains(result.DetectedFields, f => f.PiiType == "EMAIL" && f.MaskedValue == "id***@exemplo.com");
        Assert.Contains(result.DetectedFields, f => f.PiiType == "PHONE" && f.MaskedValue == "*****-4321");
        Assert.Contains(result.DetectedFields, f => f.PiiType == "HEALTH_DATA" && f.MaskedValue == "[DADO_DE_SAUDE_PROTEGIDO]");
        Assert.DoesNotContain("123.456.789-00", result.SanitizedPayload);
        Assert.DoesNotContain("12.345.678-9", result.SanitizedPayload);
        Assert.DoesNotContain("MinhaSenhaSecreta123", result.SanitizedPayload);
        Assert.DoesNotContain("Diabetes tipo 2", result.SanitizedPayload);
    }

    [Fact]
    public void ScanJsonPayload_WithoutPii_ReturnsCleanContext()
    {
        // Preparação
        var json = """
        {
            "type": "WaterLevel",
            "level": "Alert",
            "waterLevelCm": 45
        }
        """;

        // Execução
        var result = _service.ScanJsonPayload(json);

        // Verificação
        Assert.False(result.HasSensitiveData);
        Assert.Empty(result.DetectedFields);
    }

    [Fact]
    public void ScanJsonPayload_WithMalformedMqttPayloadContainingCpf_AppliesRawRegexFallback()
    {
        // Preparação
        var malformedPayload = "payload mqtt inválido com cpf 123.456.789-00 e diabetes";

        // Execução
        var result = _service.ScanJsonPayload(malformedPayload);

        // Verificação
        Assert.True(result.HasSensitiveData);
        Assert.Contains(result.DetectedFields, f => f.PiiType == "CPF" && f.MaskedValue == "***.456.***-00");
        Assert.Contains(result.DetectedFields, f => f.PiiType == "HEALTH_DATA");
        Assert.DoesNotContain("123.456.789-00", result.SanitizedPayload);
    }

    [Fact]
    public void ContainsTaintedPlaintext_WithLeakedRawData_ReturnsTrue()
    {
        // Preparação
        var json = """{ "documento": "123.456.789-00", "laudo": "Paciente usa insulina" }""";
        var taintedContext = _service.ScanJsonPayload(json);

        // Execução & Verificação
        Assert.True(_service.ContainsTaintedPlaintext("Erro ao gravar documento 123.456.789-00", taintedContext));
        Assert.True(_service.ContainsTaintedPlaintext("Falha no perfil: Paciente usa insulina", taintedContext));
        Assert.False(_service.ContainsTaintedPlaintext("Erro ao gravar documento ***.456.***-00", taintedContext));
    }

    [Fact]
    public void ContainsTaintedPlaintext_WithoutContext_StillBlocksDirectCpfAndRg()
    {
        // Execução & Verificação
        Assert.True(_service.ContainsTaintedPlaintext("Vazamento direto de 987.654.321-11", null));
        Assert.True(_service.ContainsTaintedPlaintext("Documento exposto: 12.345.678-X", null));
        Assert.False(_service.ContainsTaintedPlaintext("Operação finalizada sem exposição.", null));
    }
}