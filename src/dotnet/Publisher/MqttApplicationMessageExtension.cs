namespace Publisher;

using MQTTnet;
using MQTTnet.Packets;

public static class MqttApplicationMessageExtension
{
    private const string TimestampKey = "timestamp";

    public static MqttApplicationMessage WithUnixTimestampUserProperty(this MqttApplicationMessage message)
    {
        message.UserProperties ??= new List<MqttUserProperty>();

        var timestamp = message.UserProperties.FirstOrDefault(x => x.Name.Equals(TimestampKey));

        if (timestamp != null)
        {
            message.UserProperties.Remove(timestamp);
        }

        var unixEpochTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        message.UserProperties.Add(new MqttUserProperty(TimestampKey, $"{unixEpochTicks}"));

        return message;
    }
}