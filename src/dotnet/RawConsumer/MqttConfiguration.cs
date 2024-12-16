namespace RawConsumer;

using MQTTnet.Protocol;

/// <summary>
/// MQTT Configuration.
/// </summary>
public class MqttConfiguration
{
    /// <summary>
    /// Gets or sets MQTT Broker Port.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets MQTT Broker Host.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets MQTT Client Id.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets username for connection to broker.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets password for connection to broker.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets web socket connection path for connection to broker.
    /// </summary>
    public string WebSocketPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether connection between client and broker should use persistent session.
    /// </summary>
    public bool UsePersistentSession { get; set; } = false;

    /// <summary>
    /// Gets or sets a MqttQualityOfServiceLevel.
    /// </summary>
    public MqttQualityOfServiceLevel MqttQualityOfServiceLevel { get; set; } = MqttQualityOfServiceLevel.AtLeastOnce;

    /// <summary>
    /// Gets or sets a session expiry interval.
    /// </summary>
    public TimeSpan SessionExpiryInterval { get; set; } = TimeSpan.FromDays(5);
}