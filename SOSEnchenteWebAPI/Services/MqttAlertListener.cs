using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MQTTnet;
using MQTTnet.Client;
using SOSEnchenteWebAPI.Domain.DTOs;
using SOSEnchenteWebAPI.Hubs;

namespace SOSEnchenteWebAPI.Services;

public class MqttAlertListener : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHubContext<AlertHub> _hubContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MqttAlertListener> _logger;

    public MqttAlertListener(IHubContext<AlertHub> hubContext, IConfiguration configuration, ILogger<MqttAlertListener> logger)
    {
        _hubContext = hubContext;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = _configuration["Mqtt:Host"];
        var port = int.Parse(_configuration["Mqtt:Port"] ?? "1883");
        var topic = _configuration["Mqtt:Topic"];

        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId($"sos-enchentes-api-{Guid.NewGuid():N}")
            .WithCleanSession()
            .Build();

        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic(topic))
            .Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(options, stoppingToken);
                    await client.SubscribeAsync(subscribeOptions, stoppingToken);
                    _logger.LogInformation("MQTT conectado. Escutando o tópico {Topic}", topic);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Não foi possível conectar ao MQTT: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        if (e.ApplicationMessage.Retain)
        {
            _logger.LogInformation("Mensagem retida ignorada.");
            return;
        }

        var payload = e.ApplicationMessage.ConvertPayloadToString();

        AlertMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<AlertMessage>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            _logger.LogWarning("Mensagem MQTT inválida: {Payload}", payload);
            return;
        }

        if (message == null || message.Type != "WaterLevel")
        {
            return;
        }

        if (message.Level == "Normal")
        {
            _logger.LogInformation("Nível normalizado.");
            await _hubContext.Clients.All.SendAsync("AlertCleared");
            return;
        }

        var alert = new AlertDto
        {
            Type = message.Type,
            Level = message.Level,
            DistanceCm = message.WaterLevelCm,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Alerta recebido: {Level}, {Distance} cm", alert.Level, alert.DistanceCm);
        await _hubContext.Clients.All.SendAsync("NewAlert", alert);
    }
}