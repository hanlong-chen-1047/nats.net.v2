using System.Buffers;
using System.Threading.Channels;

namespace NATS.Client.Core;

public abstract class NatsSubBase : INatsSub
{
    internal NatsSubBase(NatsConnection connection, SubscriptionManager manager, string subject, string? queueGroup, int sid)
    {
        Connection = connection;
        Manager = manager;
        Subject = subject;
        QueueGroup = queueGroup;
        Sid = sid;
    }

    public string Subject { get; }

    public string? QueueGroup { get; }

    public int Sid { get; }

    internal NatsConnection Connection { get; }

    internal SubscriptionManager Manager { get; }

    public virtual ValueTask DisposeAsync()
    {
        return Manager.RemoveAsync(Sid);
    }

    public abstract ValueTask ReceiveAsync(string subject, string? replyTo, in ReadOnlySequence<byte>? headersBuffer, in ReadOnlySequence<byte> payloadBuffer);
}

public sealed class NatsSub : NatsSubBase
{
    private readonly Channel<NatsMsg> _msgs = Channel.CreateBounded<NatsMsg>(new BoundedChannelOptions(1_000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = false,
        AllowSynchronousContinuations = false,
    });

    internal NatsSub(NatsConnection connection, SubscriptionManager manager, string subject, string? queueGroup, int sid)
        : base(connection, manager, subject, queueGroup, sid)
    {
    }

    public ChannelReader<NatsMsg> Msgs => _msgs.Reader;

    public override ValueTask DisposeAsync()
    {
        _msgs.Writer.TryComplete();
        return base.DisposeAsync();
    }

    public override ValueTask ReceiveAsync(string subject, string? replyTo, in ReadOnlySequence<byte>? headersBuffer, in ReadOnlySequence<byte> payloadBuffer)
    {
        var natsMsg = NatsMsg.Build(
            subject,
            replyTo,
            headersBuffer,
            payloadBuffer,
            Connection,
            Connection.HeaderParser);

        return _msgs.Writer.WriteAsync(natsMsg);
    }
}

public sealed class NatsSub<T> : NatsSubBase
{
    private readonly Channel<NatsMsg<T>> _msgs = Channel.CreateBounded<NatsMsg<T>>(new BoundedChannelOptions(capacity: 1_000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = false,
        AllowSynchronousContinuations = false,
    });

    internal NatsSub(NatsConnection connection, SubscriptionManager manager, string subject, string? queueGroup, int sid, INatsSerializer serializer)
        : base(connection, manager, subject, queueGroup, sid) => Serializer = serializer;

    public ChannelReader<NatsMsg<T>> Msgs => _msgs.Reader;

    private INatsSerializer Serializer { get; }

    public override ValueTask DisposeAsync()
    {
        _msgs.Writer.TryComplete();
        return base.DisposeAsync();
    }

    public override ValueTask ReceiveAsync(string subject, string? replyTo, in ReadOnlySequence<byte>? headersBuffer, in ReadOnlySequence<byte> payloadBuffer)
    {
        var natsMsg = NatsMsg<T>.Build(
            subject,
            replyTo,
            headersBuffer,
            payloadBuffer,
            Connection,
            Connection.HeaderParser,
            Serializer);

        return _msgs.Writer.WriteAsync(natsMsg);
    }
}
