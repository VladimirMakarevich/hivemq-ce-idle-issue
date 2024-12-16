#pragma warning disable SA1600
#pragma warning disable SA1309
#pragma warning disable SA1101
#pragma warning disable SA1116
#pragma warning disable SA1515

namespace RawConsumer;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

public class Payload
{
    public string X { get; set; }

    public string Value { get; set; }

    public string Processed { get; set; }
}

public class SubscribingHostedService : BackgroundService
{
    private const int MaxClients = 1;
    private const int SubscriptionCount = 5;

    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly List<IMqttClient> _clients = new List<IMqttClient>();

    private readonly MqttConfiguration _configuration = new MqttConfiguration
    {
        Host = "localhost",
        Port = 1883,
        Username = "admin",
        Password = "hivemq",
        ClientId = "RawSubscriber",
        UsePersistentSession = true,
    };

    private readonly ILogger<SubscribingHostedService> _logger;

    public SubscribingHostedService()
    {
        using var loggerFactory = LoggerFactory.Create(x => x.SetMinimumLevel(LogLevel.Warning).AddConsole());
        _logger = loggerFactory.CreateLogger<SubscribingHostedService>();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var client in _clients)
        {
            await client.DisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection, cancellationToken: cancellationToken);
            client.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < MaxClients; i++)
        {
            await ClientConnectAsync(i, cancellationToken);
        }
    }

    private async Task ClientConnectAsync(int i, CancellationToken cancellationToken)
    {
        var enums = Enumerable.Range(1, SubscriptionCount).Select(x => x.ToString("D4")).ToList();

        var client = await ConnectAsync(i, cancellationToken);

        foreach (var x in enums)
        {
            await client.SubscribeAsync($"$share/overloadtest/overload/ce/{x}",
                                        MqttQualityOfServiceLevel.AtLeastOnce,
                                        cancellationToken);

            _logger.LogWarning($"Subscribed to $share/overload/{x} Duration {DateTime.UtcNow - _startTime}");
        }

        _logger.LogWarning($"Subscribed. Duration {DateTime.UtcNow - _startTime}, Datetime: {DateTime.UtcNow}.");
    }

    private async Task<IMqttClient> ConnectAsync(int i, CancellationToken cancellationToken)
    {
        _configuration.ClientId = $"{_configuration.ClientId}_ce_{i}";
        var client = new MqttFactory().CreateMqttClient();

        var options = GetOptions(_configuration);

        client.ConnectedAsync += _ =>
                                 {
                                     _logger.LogInformation("Client {clientId}: Connected to MQTT Broker {brokerHost}:{brokerPort}",
                                                            options.ClientId,
                                                            _configuration.Host,
                                                            _configuration.Port);
                                     return Task.CompletedTask;
                                 };

        await client.ConnectAsync(options, cancellationToken);

        client.DisconnectedAsync += _ =>
                                    {
                                        _logger.LogInformation("Client {clientId}: Disconnected from MQTT Broker {brokerHost}:{brokerPort}",
                                                               options.ClientId,
                                                               _configuration.Host,
                                                               _configuration.Port);
                                        return Task.CompletedTask;
                                    };

        var handler = new Handler();

        client.ApplicationMessageReceivedAsync += handler.HandleTopic;

        _clients.Add(client);

        return client;
    }

    private async Task<IMqttClient> Connect(MqttConfiguration configuration, CancellationToken cancellationToken)
    {
        var client = new MqttFactory().CreateMqttClient();

        var options = GetOptions(configuration);

        client.ConnectedAsync += _ =>
                                 {
                                     _logger.LogInformation("Client {clientId}: Connected to MQTT Broker {brokerHost}:{brokerPort}",
                                                            options.ClientId,
                                                            _configuration.Host,
                                                            _configuration.Port);
                                     return Task.CompletedTask;
                                 };

        await client.ConnectAsync(options, cancellationToken);

        client.DisconnectedAsync += _ =>
                                    {
                                        _logger.LogError("Client {clientId}: Disconnected from MQTT Broker {brokerHost}:{brokerPort}",
                                                         options.ClientId,
                                                         _configuration.Host,
                                                         _configuration.Port);
                                        return Task.CompletedTask;
                                    };

        return client;
    }

    private MqttClientOptions GetOptions(MqttConfiguration clientConfigurations)
    {
        var builder = new MqttClientOptionsBuilder()
                      .WithProtocolVersion(MqttProtocolVersion.V500)
                      .WithCleanSession(!clientConfigurations.UsePersistentSession)
                      .WithSessionExpiryInterval(Convert.ToUInt32(clientConfigurations.SessionExpiryInterval.TotalSeconds));

        if (!string.IsNullOrWhiteSpace(clientConfigurations.WebSocketPath))
        {
            var connectionUrl = $"{clientConfigurations.Host}:{clientConfigurations.Port}/{clientConfigurations.WebSocketPath}";
            builder.WithWebSocketServer(connectionUrl);
        }
        else
        {
            builder.WithTcpServer(clientConfigurations.Host, clientConfigurations.Port);
        }

        if (!string.IsNullOrWhiteSpace(clientConfigurations.ClientId))
        {
            builder.WithClientId(clientConfigurations.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(clientConfigurations.Username) && !string.IsNullOrWhiteSpace(clientConfigurations.Password))
        {
            builder.WithCredentials(clientConfigurations.Username, clientConfigurations.Password);
        }

        return builder.Build();
    }
}