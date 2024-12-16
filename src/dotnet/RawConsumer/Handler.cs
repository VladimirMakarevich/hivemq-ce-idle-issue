#pragma warning disable SA1600
#pragma warning disable SA1309
#pragma warning disable SA1101
#pragma warning disable SA1116
#pragma warning disable SA1203
#pragma warning disable SA1512
namespace RawConsumer;

using Microsoft.Extensions.Logging;
using MQTTnet.Client;

public class Handler
{
    private readonly ILogger<SubscribingHostedService> _logger;
    private readonly DateTime _startTime = DateTime.UtcNow;
    private int _processedMessages;

    public Handler()
    {
        using var loggerFactory = LoggerFactory.Create(x => x.SetMinimumLevel(LogLevel.Warning).AddConsole());
        _logger = loggerFactory.CreateLogger<SubscribingHostedService>();
    }

    public Task HandleTopic(MqttApplicationMessageReceivedEventArgs message)
    {
        var payload = MqttMessagePayloadSerializer.Deserialize<Payload>(message.ApplicationMessage.Payload);

        _logger.LogWarning($"Shared processed counter: [{++_processedMessages}], Topic: [{message.ApplicationMessage.Topic}], PacketIdentifier [{message.PacketIdentifier}], Message Datetime: [{payload.Value}], Datetime: [{DateTime.UtcNow.ToString("O")}].");

        return Task.CompletedTask;
    }
}