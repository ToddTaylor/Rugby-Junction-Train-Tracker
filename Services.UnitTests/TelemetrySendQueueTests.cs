using System.Collections.Concurrent;

namespace Services.UnitTests;

[TestClass]
public class TelemetrySendQueueTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<string> _log = new();

    private TelemetrySendQueue CreateQueue(string subscriberId = "Test", int capacity = 100, TimeSpan? maxAge = null, Func<DateTime>? utcNow = null) =>
        new(subscriberId, capacity, maxAge ?? TimeSpan.FromMinutes(5), TimeSpan.Zero, utcNow, _log.Enqueue);

    [TestMethod]
    public async Task OneUnreachableSubscriber_DoesNotHoldUpTheOthers()
    {
        // The acceptance case: while one API never answers, another still gets every packet within seconds.
        var neverAnswers = new TaskCompletionSource();
        var stuck = CreateQueue("Unreachable");
        var healthy = CreateQueue("Healthy");
        var delivered = new ConcurrentQueue<int>();

        for (var i = 1; i <= 3; i++)
        {
            var packet = i;
            stuck.Enqueue(() => neverAnswers.Task);
            healthy.Enqueue(() => { delivered.Enqueue(packet); return Task.CompletedTask; });
        }

        healthy.Complete();
        await healthy.Completion.WaitAsync(WaitLimit);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, delivered.ToArray());
        Assert.IsFalse(stuck.Completion.IsCompleted);
        neverAnswers.SetResult();
    }

    [TestMethod]
    public async Task Enqueue_ReturnsWithoutWaitingForTheSend()
    {
        var neverAnswers = new TaskCompletionSource();
        var queue = CreateQueue();

        var enqueue = Task.Run(() =>
        {
            for (var i = 0; i < 10; i++)
            {
                queue.Enqueue(() => neverAnswers.Task);
            }
        });

        await enqueue.WaitAsync(WaitLimit);
        neverAnswers.SetResult();
    }

    [TestMethod]
    public async Task Packets_AreSentOneAtATimeInOrder()
    {
        var queue = CreateQueue();
        var sent = new List<int>();
        var running = 0;
        var overlapped = false;

        for (var i = 1; i <= 20; i++)
        {
            var packet = i;
            queue.Enqueue(async () =>
            {
                overlapped |= Interlocked.Increment(ref running) > 1;
                await Task.Yield();
                sent.Add(packet);
                Interlocked.Decrement(ref running);
            });
        }

        queue.Complete();
        await queue.Completion.WaitAsync(WaitLimit);

        CollectionAssert.AreEqual(Enumerable.Range(1, 20).ToArray(), sent);
        Assert.IsFalse(overlapped);
    }

    [TestMethod]
    public async Task ASendThatThrows_IsLogged_AndTheNextPacketIsStillSent()
    {
        var queue = CreateQueue();
        var sent = new List<int>();

        queue.Enqueue(() => throw new InvalidOperationException("boom"));
        queue.Enqueue(() => { sent.Add(2); return Task.CompletedTask; });
        queue.Complete();
        await queue.Completion.WaitAsync(WaitLimit);

        CollectionAssert.AreEqual(new[] { 2 }, sent);
        Assert.IsTrue(_log.Any(line => line.Contains("[Test]") && line.Contains("boom")));
    }

    [TestMethod]
    public async Task APacketThatWaitedTooLong_IsDropped_AndReported()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var release = new TaskCompletionSource();
        var queue = CreateQueue(maxAge: TimeSpan.FromMinutes(5), utcNow: () => now);
        var sent = new List<int>();

        // The first send holds the worker while the clock moves on past the age limit.
        queue.Enqueue(() => release.Task);
        queue.Enqueue(() => { sent.Add(2); return Task.CompletedTask; });
        now = now.AddMinutes(6);
        queue.Enqueue(() => { sent.Add(3); return Task.CompletedTask; });
        release.SetResult();

        queue.Complete();
        await queue.Completion.WaitAsync(WaitLimit);

        CollectionAssert.AreEqual(new[] { 3 }, sent);
        Assert.IsTrue(_log.Any(line => line.Contains("Dropped 1 telemetry packet")));
    }

    [TestMethod]
    public async Task AFullQueue_DropsTheOldestPacket()
    {
        var release = new TaskCompletionSource();
        var queue = CreateQueue(capacity: 2);
        var sent = new List<int>();

        queue.Enqueue(() => release.Task);
        // Give the worker time to take the first packet, so the queue itself is empty.
        await WaitUntil(() => queue.Count == 0);

        for (var i = 1; i <= 4; i++)
        {
            var packet = i;
            queue.Enqueue(() => { sent.Add(packet); return Task.CompletedTask; });
        }

        release.SetResult();
        queue.Complete();
        await queue.Completion.WaitAsync(WaitLimit);

        CollectionAssert.AreEqual(new[] { 3, 4 }, sent);
        Assert.IsTrue(_log.Any(line => line.Contains("Dropped 2 telemetry packet")));
    }

    [TestMethod]
    public void ForSubscriber_GivesEachSubscriberOneQueue()
    {
        var first = TelemetrySendQueue.ForSubscriber("QueueTests-A");

        Assert.AreSame(first, TelemetrySendQueue.ForSubscriber("QueueTests-A"));
        Assert.AreNotSame(first, TelemetrySendQueue.ForSubscriber("QueueTests-B"));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }
}
