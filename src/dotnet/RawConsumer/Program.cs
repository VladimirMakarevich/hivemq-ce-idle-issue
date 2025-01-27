﻿namespace RawConsumer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class Program
{
    public static async Task Main(string[] args)
    {
        using var connectionTimeoutSource = new CancellationTokenSource();
        var cancellationToken = connectionTimeoutSource.Token;

        var host = new HostBuilder()
                   .ConfigureServices(services => services.AddHostedService<SubscribingHostedService>())
                   .Build();

        await host.RunAsync(cancellationToken);
    }
}