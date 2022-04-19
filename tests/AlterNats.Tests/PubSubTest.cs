﻿using Cysharp.Diagnostics;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace AlterNats.Tests;

public class PubSubTest : IDisposable
{
    readonly ITestOutputHelper output;
    readonly TimeSpan timeout;

    public PubSubTest(ITestOutputHelper output)
    {
        this.output = output;
        this.timeout = TimeSpan.FromSeconds(5);
    }

    [Fact]
    public async Task SimplePubSubTest()
    {
        await using var server = new NatsServer(output);

        await using var subConnection = server.CreateClientConnection();
        await using var pubConnection = server.CreateClientConnection();

        var key = new NatsKey(Guid.NewGuid().ToString("N"));
        TaskCompletionSource signalComplete = new TaskCompletionSource();

        var list = new List<int>();
        await subConnection.SubscribeAsync<int>(key, x =>
        {
            output.WriteLine($"Received: {x}");
            list.Add(x);
            if (x == 9)
            {
                signalComplete.SetResult();
            }
        });

        for (int i = 0; i < 10; i++)
        {
            await pubConnection.PublishAsync(key, i);
        }

        await signalComplete.Task.WaitAsync(timeout);

        list.ShouldEqual(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
    }

    [Fact]
    public async Task ReconnectTest()
    {
        await using var cluster = new NatsCluster(output);

        await using var connection1 = cluster.Server1.CreateClientConnection();
        await using var connection2 = cluster.Server2.CreateClientConnection();
        await using var connection3 = cluster.Server3.CreateClientConnection();

        await connection1.ConnectAsync();
        await connection2.ConnectAsync();
        await connection3.ConnectAsync();

        var disocnnectSignal = new TaskCompletionSource();
        connection1.ConnectionDisconnected += (_, __) =>
        {
            disocnnectSignal.SetResult();
        };

        output.WriteLine($"TRY KILL SERVER1 Port:{cluster.Server1.Port}");
        await cluster.Server1.DisposeAsync();
        await disocnnectSignal.Task.WaitAsync(timeout);

        await connection1.ConnectAsync(); // wait for reconnect complete.

        connection1.ServerInfo!.Port.Should().BeOneOf(cluster.Server2.Port, cluster.Server3.Port);
    }


    //    [Theory]
    //    [MemberData(nameof(BasicTestData))]
    //    public async Task Basic<T>(int subPort, int pubPort, IEnumerable<T> items)
    //    {
    //        AutoResetEvent autoResetEvent = new AutoResetEvent(false);

    //        autoResetEvent.Reset();
    //        List<T> results = new();

    //        var natsKey = new NatsKey(Guid.NewGuid().ToString("N"));

    //        await using var subConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + subPort
    //        });

    //        await subConnection.ConnectAsync();

    //        using var d = await subConnection.SubscribeAsync<T>(natsKey, x =>
    //        {
    //            results.Add(x);

    //            if (results.Count == items.Count())
    //                autoResetEvent.Set();
    //        });

    //        await using var pubConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + pubPort
    //        });

    //        await pubConnection.ConnectAsync();

    //        foreach (var item in items)
    //        {
    //            await pubConnection.PublishAsync(natsKey, item);
    //        }

    //        var waitResult = autoResetEvent.WaitOne(5000);

    //        Assert.True(waitResult, "Timeout");
    //        Assert.Equal(items.ToArray(), results.ToArray());
    //    }

    //    [Theory]
    //    [MemberData(nameof(BasicTestData))]
    //    public async Task BasicWithMessagePackSerializer<T>(int subPort, int pubPort, IEnumerable<T> items)
    //    {
    //        AutoResetEvent autoResetEvent = new AutoResetEvent(false);

    //        autoResetEvent.Reset();
    //        List<T> results = new();

    //        var natsKey = new NatsKey(Guid.NewGuid().ToString("N"));

    //        await using var subConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + subPort,
    //            Serializer = new MessagePackNatsSerializer()
    //        });

    //        await subConnection.ConnectAsync();

    //        using var d = await subConnection.SubscribeAsync<T>(natsKey, x =>
    //        {
    //            results.Add(x);

    //            if (results.Count == items.Count())
    //                autoResetEvent.Set();
    //        });

    //        await using var pubConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + pubPort,
    //            Serializer = new MessagePackNatsSerializer()
    //        });

    //        await pubConnection.ConnectAsync();

    //        foreach (var item in items)
    //        {
    //            await pubConnection.PublishAsync(natsKey, item);
    //        }

    //        var waitResult = autoResetEvent.WaitOne(5000);

    //        Assert.True(waitResult, "Timeout");
    //        Assert.Equal(items.ToArray(), results.ToArray());
    //    }

    //    [Theory]
    //    [MemberData(nameof(BasicTestData))]
    //    public async Task BasicRequest<T>(int subPort, int pubPort, IEnumerable<T> items)
    //    {
    //        var natsKey = new NatsKey(Guid.NewGuid().ToString("N"));

    //        await using var subConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + subPort
    //        });

    //        await subConnection.ConnectAsync();

    //        using var d = await subConnection.SubscribeRequestAsync<T, string>(natsKey, x => $"Re{x}");

    //        await using var pubConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + pubPort
    //        });

    //        await pubConnection.ConnectAsync();

    //        foreach (var item in items)
    //        {
    //            Assert.Equal($"Re{item}", await pubConnection.RequestAsync<T, string>(natsKey, item));
    //        }
    //    }

    //    [Theory]
    //    [MemberData(nameof(BasicTestData))]
    //    public async Task BasicRequestWithMessagePackSerializer<T>(int subPort, int pubPort, IEnumerable<T> items)
    //    {
    //        var natsKey = new NatsKey(Guid.NewGuid().ToString("N"));

    //        await using var subConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + subPort,
    //            Serializer = new MessagePackNatsSerializer()
    //        });

    //        await subConnection.ConnectAsync();

    //        using var d = await subConnection.SubscribeRequestAsync<T, string>(natsKey, x => $"Re{x}");

    //        await using var pubConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + pubPort,
    //            Serializer = new MessagePackNatsSerializer()
    //        });

    //        await pubConnection.ConnectAsync();

    //        foreach (var item in items)
    //        {
    //            Assert.Equal($"Re{item}", await pubConnection.RequestAsync<T, string>(natsKey, item));
    //        }
    //    }

    //    [Theory]
    //    [MemberData(nameof(SubjectTestData))]
    //    public async Task SubjectTest(string pubKey, int? expect1, int? expect2, int? expect3, int? expect4, int? expect5, int? expect6)
    //    {
    //        AutoResetEvent autoResetEvent1 = new AutoResetEvent(false);
    //        AutoResetEvent autoResetEvent2 = new AutoResetEvent(false);
    //        AutoResetEvent autoResetEvent3 = new AutoResetEvent(false);
    //        AutoResetEvent autoResetEvent4 = new AutoResetEvent(false);
    //        AutoResetEvent autoResetEvent5 = new AutoResetEvent(false);
    //        AutoResetEvent autoResetEvent6 = new AutoResetEvent(false);

    //        int? result1 = null;
    //        int? result2 = null;
    //        int? result3 = null;
    //        int? result4 = null;
    //        int? result5 = null;
    //        int? result6 = null;

    //        await using var subConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + primaryPort,
    //        });

    //        await subConnection.ConnectAsync();

    //        using var d1 = await subConnection.SubscribeAsync<int>(">", x =>
    //        {
    //            result1 = x;
    //            autoResetEvent1.Set();
    //        });

    //        using var d2 = await subConnection.SubscribeAsync<int>("*", x =>
    //        {
    //            result2 = x;
    //            autoResetEvent2.Set();
    //        });

    //        using var d3 = await subConnection.SubscribeAsync<int>("subject", x =>
    //        {
    //            result3 = x;
    //            autoResetEvent3.Set();
    //        });

    //        using var d4 = await subConnection.SubscribeAsync<int>("subject.*", x =>
    //        {
    //            result4 = x;
    //            autoResetEvent4.Set();
    //        });

    //        using var d5 = await subConnection.SubscribeAsync<int>("subject.>", x =>
    //        {
    //            result5 = x;
    //            autoResetEvent5.Set();
    //        });

    //        using var d6 = await subConnection.SubscribeAsync<int>("other", x =>
    //        {
    //            result6 = x;
    //            autoResetEvent6.Set();
    //        });

    //        await using var pubConnection = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + primaryPort,
    //        });

    //        await pubConnection.ConnectAsync();

    //        await pubConnection.PublishAsync(new NatsKey(pubKey), 99);

    //        WaitHandle.WaitAll(new WaitHandle[]
    //        {
    //            autoResetEvent1,
    //            autoResetEvent2,
    //            autoResetEvent3,
    //            autoResetEvent4,
    //            autoResetEvent5,
    //            autoResetEvent6
    //        }, 1000);

    //        Assert.Equal(expect1, result1);
    //        Assert.Equal(expect2, result2);
    //        Assert.Equal(expect3, result3);
    //        Assert.Equal(expect4, result4);
    //        Assert.Equal(expect5, result5);
    //        Assert.Equal(expect6, result6);
    //    }

    //    [Fact]
    //    public async Task ConnectionException()
    //    {
    //        var connection1 = new NatsConnection(NatsOptions.Default with
    //        {
    //            Url = "localhost:" + 14250,
    //        });

    //        await Assert.ThrowsAsync<NatsException>(async () => await connection1.ConnectAsync());
    //    }

    //    static readonly int[] seed1 = { 24, 45, 99, 41, 98, 7, 81, 8, 26, 56 };

    //    static int primaryPort = 14222;
    //    static int secondaryPort = 14223;

    //    static object[][] BasicTestData()
    //    {
    //        return new[]
    //        {
    //            new object[] { primaryPort, primaryPort, seed1 },
    //            new object[] { primaryPort, secondaryPort, seed1 },
    //            new object[] { secondaryPort, primaryPort, seed1 },
    //            new object[] { secondaryPort, secondaryPort, seed1 },
    //            new object[] { primaryPort, primaryPort, seed1.Select(x => $"Test:{x}") },
    //            new object[] { primaryPort, primaryPort, seed1.Select(x => new SampleClass(x, $"Name{x}")) }
    //        };
    //    }

    //    static object?[][] SubjectTestData()
    //    {
    //        return new[]
    //        {
    //            new object?[] { "subject", 99, 99, 99, null, null, null },
    //            new object?[] { "subject.a", 99, null, null, 99, 99, null },
    //            new object?[] { "subject.a.b", 99, null, null, null, 99, null },
    //            new object?[] { "other", 99, 99, null, null, null, 99 },
    //        };
    //    }

    //    CancellationTokenSource cancellationTokenSource = new();

    //    [Fact]
    //    public async Task ReConnectionTest()
    //    {
    //#pragma warning disable CS4014

    //        var cancellationTokenSource2 = new CancellationTokenSource();

    //        var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

    //        // Start the third nats server
    //        ProcessX
    //            .StartAsync($"../../../../../tools/nats-server{ext} -p 14224 -cluster nats://localhost:14250 -routes nats://localhost:14248 --cluster_name test-cluster")
    //            .WaitAsync(cancellationTokenSource.Token);

    //        Task.Run(async () =>
    //        {
    //            using var client = new TcpClient();

    //            while (true)
    //            {
    //                try
    //                {
    //                    client.Connect("localhost", 14224);

    //                    if (client.Connected) break;
    //                }
    //                catch
    //                {
    //                    // ignore
    //                }

    //                await Task.Delay(500);
    //            }
    //        }).Wait(5000);

    //        var connection = new NatsConnection(NatsOptions.Default with
    //        {
    //            LoggerFactory = new OutputHelperLoggerFactory(output),
    //            Url = "localhost:" + 14224,
    //        });

    //        // Wait for start clustering.
    //        Task.Run(async () =>
    //        {
    //            while (true)
    //            {
    //                await connection.ConnectAsync();

    //                if (connection.ServerInfo?.ClientConnectUrls != null) break;

    //                connection = new NatsConnection(NatsOptions.Default with
    //                {
    //                    LoggerFactory = new OutputHelperLoggerFactory(output),
    //                    Url = "localhost:" + 14224,
    //                });

    //                await Task.Delay(500);
    //            }
    //        }).Wait(5000);

    //        // If the third server can not start clustering, test failed
    //        Assert.NotNull(connection.ServerInfo?.ClientConnectUrls);

    //        var natsKey = new NatsKey(Guid.NewGuid().ToString("N"));

    //        using var sub = await connection.SubscribeAsync<int>(natsKey, _ => { });

    //        Task.Run(async () =>
    //        {
    //            while (!cancellationTokenSource2.IsCancellationRequested)
    //            {
    //                await connection.PublishAsync(natsKey, int.MinValue);
    //                await Task.Delay(1000);
    //            }
    //        });

    //        Assert.Equal(NatsConnectionState.Open, connection.ConnectionState);
    //        Assert.NotNull(connection.ServerInfo);
    //        Assert.Equal(14224, connection.ServerInfo!.Port);

    //        // Shutdown the third server
    //        cancellationTokenSource.Cancel();

    //        // Check for shutdown the third server.
    //        Task.Run(async () =>
    //        {
    //            using var client = new TcpClient();

    //            while (true)
    //            {
    //                try
    //                {
    //                    client.Connect("localhost", 14224);
    //                }
    //                catch
    //                {
    //                    break;
    //                }

    //                await Task.Delay(500);
    //            }
    //        }).Wait(5000);

    //        // Wait for reconnect
    //        Task.Run(async () =>
    //        {
    //            while (connection.ConnectionState != NatsConnectionState.Open)
    //            {
    //                await Task.Delay(500);
    //            }
    //        }).Wait(5000);

    //        Assert.Equal(NatsConnectionState.Open, connection.ConnectionState);
    //        Assert.NotEqual(14224, connection.ServerInfo.Port);

    //        await connection.DisposeAsync();
    //        cancellationTokenSource2.Cancel();

    //#pragma warning restore CS4014
    //    }

    public void Dispose()
    {
    }
}

[MessagePackObject]
public class SampleClass : IEquatable<SampleClass>
{
    [Key(0)]
    public int Id { get; set; }
    [Key(1)]
    public string Name { get; set; }

    public SampleClass(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public bool Equals(SampleClass? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id == other.Id && Name == other.Name;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((SampleClass)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name);
    }

    public override string ToString()
    {
        return $"{Id}-{Name}";
    }
}
