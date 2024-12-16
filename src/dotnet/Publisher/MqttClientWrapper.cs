namespace Publisher;

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;

public class MqttClientWrapper
{
    private IMqttClient _mqttClient;

    public MqttClientWrapper(MqttConfiguration configurations,
                             ILogger<MqttClientWrapper> logger)
    {
        Configuration = configurations;
        Logger = logger;
        Options = SetOptions(configurations);
    }

    private MqttConfiguration Configuration { get; }

    private ILogger<MqttClientWrapper> Logger { get; }

    private MqttClientOptions Options { get; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var mqttClient = new MqttFactory().CreateMqttClient();

            mqttClient.ConnectedAsync += _ =>
                                         {
                                             Logger.LogInformation(
                                                                   "Client {clientId}: Connected to MQTT Broker {brokerHost}:{brokerPort}",
                                                                   Options.ClientId,
                                                                   Configuration.Host,
                                                                   Configuration.Port);
                                             return Task.CompletedTask;
                                         };

            Logger.LogInformation("Client {clientId}: Connecting to MQTT Broker {brokerHost}:{brokerPort}",
                                  Options.ClientId,
                                  Configuration.Host,
                                  Configuration.Port);

            var connectionResult = await mqttClient.ConnectAsync(Options, cancellationToken);

            if (connectionResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new
                        MqttCommunicationException($"Client tried to connect but server denied connection with reason '{connectionResult.ResultCode}'.");
            }

            _mqttClient = mqttClient;

            _mqttClient.DisconnectedAsync += _ =>
                                             {
                                                 Logger.LogInformation("Client {clientId}: Disconnected from MQTT Broker {brokerHost}:{brokerPort}",
                                                                       Options.ClientId,
                                                                       Configuration.Host,
                                                                       Configuration.Port);
                                                 return Task.CompletedTask;
                                             };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to the broker. Check the configuration. Error: [{exception}].", ex.Message);
            throw;
        }
    }

    public Task PublishAsync<T>(T payloadRaw, string topic, bool withRetainFlag, CancellationToken cancellationToken)
    {
        var payload = MqttMessagePayloadSerializer.Serialize(payloadRaw);
        var applicationMessage = new MqttApplicationMessageBuilder()
                                 .WithQualityOfServiceLevel(Configuration.MqttQualityOfServiceLevel)
                                 .WithContentType("application/json")
                                 .WithRetainFlag(withRetainFlag)
                                 .WithTopic(topic)
                                 .WithPayload(payload)
                                 .Build()
                                 .WithUnixTimestampUserProperty();

        return InternalPublishAsync(applicationMessage, cancellationToken);
    }

    private async Task InternalPublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Publishing message. Topic: {topic}, Retained: {withRetainFlag}", applicationMessage.Topic, applicationMessage.Retain);

        await _mqttClient.PublishAsync(applicationMessage, cancellationToken);

        Logger.LogDebug("Published message. Topic: {topic}, Retained: {withRetainFlag}", applicationMessage.Topic, applicationMessage.Retain);
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
    {
        Logger.LogWarning("Client: {clientId}. Disconnected from broker on {brokerHost}:{brokerPort}",
                          Configuration.ClientId,
                          Configuration.Host,
                          Configuration.Port);

        try
        {
            await _mqttClient.ReconnectAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError("Client: {clientId} unable to reconnect to {brokerHost}:{brokerPort}: {message}",
                            Configuration.ClientId,
                            Configuration.Host,
                            Configuration.Port,
                            ex.Message);
            throw;
        }

        Logger.LogWarning("Client: {clientId}. Reconnected to {brokerHost}:{brokerPort}",
                          Configuration.ClientId,
                          Configuration.Host,
                          Configuration.Port);
    }

    public async Task CloseAsync(CancellationToken cancellationToken, MqttClientDisconnectOptions options = null)
    {
        if (_mqttClient is not null)
        {
            Logger.LogInformation("Client: {clientId}. Closing connection to {brokerHost}:{brokerPort}",
                                  Configuration.ClientId,
                                  Configuration.Host,
                                  Configuration.Port);
            options ??= new MqttClientDisconnectOptions
            {
                Reason = MqttClientDisconnectOptionsReason.NormalDisconnection,
            };

            _mqttClient.DisconnectedAsync -= OnDisconnectedAsync;
            await _mqttClient.DisconnectAsync(options, cancellationToken);
            Logger.LogInformation("Client: {clientId}. Closed connection to {brokerHost}:{brokerPort}",
                                  Configuration.ClientId,
                                  Configuration.Host,
                                  Configuration.Port);
        }
    }

    private MqttClientOptions SetOptions(MqttConfiguration clientConfigurations)
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