namespace Publisher;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Publisher;

public class PublishingHostedService : BackgroundService
{
    private const int SubscriptionCount = 5;
    private const int ClientsCount = 20;
    private readonly DateTime _startTime = DateTime.UtcNow;

    private readonly IEnumerable<MqttClientWrapper> _clients;
    private readonly ILogger<PublishingHostedService> _logger;

    public PublishingHostedService()
    {
        using var loggerFactory = LoggerFactory.Create(x => x.SetMinimumLevel(LogLevel.Information).AddConsole());

        _clients = Enumerable.Range(1, ClientsCount)
                             .Select(_ => new MqttClientWrapper(BuildConfiguration(),
                                                                loggerFactory.CreateLogger<MqttClientWrapper>()))
                             .ToList();
        _logger = loggerFactory.CreateLogger<PublishingHostedService>();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var closeClientTasks = _clients.Select(x => x.CloseAsync(cancellationToken));
        await Task.WhenAll(closeClientTasks);
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var client in _clients)
        {
            await client.ConnectAsync(stoppingToken);
        }

        var enums = Enumerable.Range(1, SubscriptionCount).Select(x => x.ToString("D4")).ToList();

        for (int i = 1; !stoppingToken.IsCancellationRequested; i++)
        {
            await PublishAsync(enums, stoppingToken);
            _logger.LogWarning($"Published: {i * SubscriptionCount * _clients.Count()}: Duration {DateTime.UtcNow - _startTime}, Datetime: {DateTime.UtcNow}.");
        }
    }

    private async Task PublishAsync(List<string> enums, CancellationToken cancellationToken)
    {
        foreach (var chunk in enums.Chunk(5))
        {
            var payloads = chunk.Select(x => new Payload
            {
                X = x,
                Value = DateTime.UtcNow.ToString("O"),
            });

            var tasks =
                    _clients.SelectMany(client =>
                                                payloads.Select(payload => client.PublishAsync(payload,
                                                                                               $"overload/ce/{payload.X}",
                                                                                               false,
                                                                                               cancellationToken)));

            await Task.WhenAll(tasks);
        }
    }

    private MqttConfiguration BuildConfiguration() => new MqttConfiguration()
    {
        Host = "localhost",
        Port = 1883,
        Username = "admin",
        Password = "hivemq",
        ClientId = $"DotnetPublisher{Guid.NewGuid()}",
        UsePersistentSession = true,
    };
}

public class Payload
{
    public string X { get; set; }

    public string Value { get; set; }
}