using System.Text.Json;
using System.Text.RegularExpressions;

namespace SOSEnchenteWebAPI.Services;

// Representa um dado sensível (PII) detectado e classificado na entrada (source)
public class TaintedField
{
    public string FieldName { get; set; } = string.Empty;
    public string PiiType { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;
    public string MaskedValue { get; set; } = string.Empty;
}

// Mantém o rastreamento dos campos contaminados ao longo da requisição
public class TaintedDataContext
{
    public List<TaintedField> DetectedFields { get; } = new();
    public string SanitizedPayload { get; set; } = string.Empty;
    public bool HasSensitiveData => DetectedFields.Count > 0;
}

public interface IPiiScannerService
{
    TaintedDataContext ScanJsonPayload(string rawJson);
    string MaskSensitiveValue(string piiType, string value);
    bool ContainsTaintedPlaintext(string outputText, TaintedDataContext? context);
}

public class PiiScannerService : IPiiScannerService
{
    // Expressões regulares para identificação de dados pessoais conforme a LGPD
    private static readonly Regex CpfRegex = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex RgRegex = new(@"\b\d{2}\.?\d{3}\.?\d{3}-?[0-9Xx]\b", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"\b[^\s@]+@[^\s@]+\.[^\s@]+\b", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"\b(?:\+?55\s?)?(?:\(?\d{2}\)?\s?)?\d{4,5}-?\d{4}\b", RegexOptions.Compiled);

    private static readonly HashSet<string> SensitiveKeyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "senha", "cpf", "rg", "documento", "document",
        "healthconditions", "laudo", "laudomedico", "medicalreport", "phonenumber", "email"
    };

    private static readonly string[] HealthKeywords =
    {
        "diabetes", "hipertens", "cardiac", "cardi", "insulina", "cadeirante",
        "acamado", "laudo", "cid", "alzheimer", "asma", "oxigenio", "marca-passo"
    };

    public TaintedDataContext ScanJsonPayload(string rawJson)
    {
        var context = new TaintedDataContext();

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return context;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            InspectElement(document.RootElement, "root", context);
        }
        catch (JsonException)
        {
            // Para payloads malformados (ex.: mensagens MQTT inválidas), aplica varredura textual direta
            InspectRawText("payload", rawJson, context);
        }

        context.SanitizedPayload = BuildSanitizedPreview(rawJson, context);
        return context;
    }

    public string MaskSensitiveValue(string piiType, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        switch (piiType)
        {
            case "CPF":
                var digits = Regex.Replace(value, @"\D", string.Empty);
                if (digits.Length == 11)
                {
                    return $"***.{digits.Substring(3, 3)}.***-{digits.Substring(9, 2)}";
                }
                return "***.***.***-**";

            case "RG":
                return "**.***.***-*";

            case "PASSWORD":
                return "********";

            case "HEALTH_DATA":
                return "[DADO_DE_SAUDE_PROTEGIDO]";

            case "EMAIL":
                var parts = value.Split('@');
                if (parts.Length == 2 && parts[0].Length > 2)
                {
                    return $"{parts[0][..2]}***@{parts[1]}";
                }
                return "***@***";

            case "PHONE":
                var phoneDigits = Regex.Replace(value, @"\D", string.Empty);
                if (phoneDigits.Length >= 4)
                {
                    return $"*****-{phoneDigits[^4..]}";
                }
                return "*****-****";

            default:
                return "[PII_MASCARADO]";
        }
    }

    public bool ContainsTaintedPlaintext(string outputText, TaintedDataContext? context)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return false;
        }

        if (context != null)
        {
            foreach (var field in context.DetectedFields)
            {
                if (!string.IsNullOrWhiteSpace(field.RawValue) &&
                    field.RawValue.Length >= 4 &&
                    outputText.Contains(field.RawValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // Bloqueio defensivo para CPF ou RG em texto claro mesmo sem contexto prévio
        return CpfRegex.IsMatch(outputText) || RgRegex.IsMatch(outputText);
    }

    private void InspectElement(JsonElement element, string currentPath, TaintedDataContext context)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var nextPath = $"{currentPath}.{property.Name}";
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        var text = property.Value.GetString() ?? string.Empty;
                        InspectProperty(property.Name, nextPath, text, context);
                    }
                    else
                    {
                        InspectElement(property.Value, nextPath, context);
                    }
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var itemPath = $"{currentPath}[{index}]";
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var text = item.GetString() ?? string.Empty;
                        InspectProperty(currentPath, itemPath, text, context);
                    }
                    else
                    {
                        InspectElement(item, itemPath, context);
                    }
                    index++;
                }
                break;
        }
    }

    private void InspectProperty(string propertyName, string fullPath, string value, TaintedDataContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalizedName = propertyName.ToLowerInvariant();

        if (normalizedName.Contains("password") || normalizedName.Contains("senha"))
        {
            AddTaintedField(context, fullPath, "PASSWORD", value);
            return;
        }

        if (normalizedName.Contains("healthcondition") || normalizedName.Contains("laudo") || normalizedName.Contains("medical"))
        {
            AddTaintedField(context, fullPath, "HEALTH_DATA", value);
            return;
        }

        if (CpfRegex.IsMatch(value))
        {
            foreach (Match match in CpfRegex.Matches(value))
            {
                AddTaintedField(context, fullPath, "CPF", match.Value);
            }
            return;
        }

        if (RgRegex.IsMatch(value))
        {
            foreach (Match match in RgRegex.Matches(value))
            {
                AddTaintedField(context, fullPath, "RG", match.Value);
            }
            return;
        }

        if (EmailRegex.IsMatch(value) || normalizedName.Contains("email"))
        {
            AddTaintedField(context, fullPath, "EMAIL", value);
            return;
        }

        if (PhoneRegex.IsMatch(value) || normalizedName.Contains("phone"))
        {
            AddTaintedField(context, fullPath, "PHONE", value);
            return;
        }

        if (SensitiveKeyNames.Contains(propertyName) ||
            HealthKeywords.Any(k => value.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            AddTaintedField(context, fullPath, "HEALTH_DATA", value);
        }
    }

    private void InspectRawText(string fieldName, string rawText, TaintedDataContext context)
    {
        foreach (Match match in CpfRegex.Matches(rawText))
        {
            AddTaintedField(context, fieldName, "CPF", match.Value);
        }

        foreach (Match match in RgRegex.Matches(rawText))
        {
            AddTaintedField(context, fieldName, "RG", match.Value);
        }

        foreach (Match match in EmailRegex.Matches(rawText))
        {
            AddTaintedField(context, fieldName, "EMAIL", match.Value);
        }

        if (HealthKeywords.Any(k => rawText.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            AddTaintedField(context, fieldName, "HEALTH_DATA", rawText);
        }
    }

    private void AddTaintedField(TaintedDataContext context, string fieldName, string piiType, string rawValue)
    {
        context.DetectedFields.Add(new TaintedField
        {
            FieldName = fieldName,
            PiiType = piiType,
            RawValue = rawValue,
            MaskedValue = MaskSensitiveValue(piiType, rawValue)
        });
    }

    private static string BuildSanitizedPreview(string rawJson, TaintedDataContext context)
    {
        var sanitized = rawJson;
        foreach (var field in context.DetectedFields)
        {
            if (!string.IsNullOrWhiteSpace(field.RawValue))
            {
                sanitized = sanitized.Replace(field.RawValue, field.MaskedValue, StringComparison.Ordinal);
            }
        }
        return sanitized.Length > 500 ? sanitized[..500] : sanitized;
    }
}