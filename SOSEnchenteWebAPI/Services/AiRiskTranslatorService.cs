using System.Text;
using System.Text.Json;

namespace SOSEnchenteWebAPI.Services;

public interface IAiRiskTranslatorService
{
    Task<string> TranslateTechnicalBlockAsync(string technicalEvent, string endpoint);
}

public class AiRiskTranslatorService : IAiRiskTranslatorService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiRiskTranslatorService> _logger;

    public AiRiskTranslatorService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AiRiskTranslatorService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> TranslateTechnicalBlockAsync(string technicalEvent, string endpoint)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";

        // Mensagem acessível de contingência caso a chave não esteja definida ou ocorra falha de rede
        const string fallbackMessage =
            "Nosso sistema identificou uma tentativa de exposição dos seus dados pessoais e bloqueou a ação automaticamente para proteger sua privacidade.";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Chave da API do Gemini não configurada. Aplicando mensagem de contingência.");
            return fallbackMessage;
        }

        var prompt =
            $"Traduza a falha técnica '{technicalEvent}' ocorrida na rota '{endpoint}' do aplicativo SOS Enchente " +
            "para um cidadão leigo de 60 anos, em no máximo 2 frases curtas e tranquilizadoras em português brasileiro, sem termos técnicos.";

        var requestPayload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
        };

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var content = new StringContent(
                JsonSerializer.Serialize(requestPayload),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Falha ao consultar API do Gemini. Status HTTP: {StatusCode}", response.StatusCode);
                return fallbackMessage;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var translatedText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return string.IsNullOrWhiteSpace(translatedText)
                ? fallbackMessage
                : translatedText.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao traduzir evento de segurança via Gemini.");
            return fallbackMessage;
        }
    }
}