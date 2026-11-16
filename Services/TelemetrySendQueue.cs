using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Services
{
    /// <summary>
    /// Sends one subscriber's telemetry in the background, one packet at a time and in the order received.
    ///
    /// The log reader hands every packet to every subscriber on one thread. When a subscriber sent from that
    /// thread, an API that was down or slow held up the reader and every other subscriber for as long as its
    /// retries took. Each subscriber now has its own queue and worker, so the reader only filters and queues.
    ///
    /// The queue holds at most <see cref="DefaultCapacity"/> packets; when it is full the oldest is dropped.
    /// A packet that has waited longer than <see cref="DefaultMaxAge"/> is dropped instead of sent, because the
    /// web server discards telemetry that old anyway.
    /// </summary>
    public sealed class TelemetrySendQueue
    {
        public const int DefaultCapacity = 500;
        public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(5);

        /// <summary>Pause after each send, so the web server is not handed a burst.</summary>
        public static readonly TimeSpan DefaultDelayBetweenSends = TimeSpan.FromMilliseconds(100);

        private static readonly TimeSpan DropReportInterval = TimeSpan.FromMinutes(1);
        private static readonly ConcurrentDictionary<string, TelemetrySendQueue> Queues = new(StringComparer.OrdinalIgnoreCase);

        private readonly string _subscriberId;
        private readonly Channel<QueuedSend> _channel;
        private readonly TimeSpan _maxAge;
        private readonly TimeSpan _delayBetweenSends;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private int _droppedSinceReport;
        private DateTime _lastDropReport = DateTime.MinValue;

        /// <summary>
        /// The queue for a subscriber, shared by its DPU and HOT/EOT handlers so packets reach its API in order.
        /// </summary>
        public static TelemetrySendQueue ForSubscriber(string subscriberId) =>
            Queues.GetOrAdd(subscriberId, id => new TelemetrySendQueue(id));

        public TelemetrySendQueue(
            string subscriberId,
            int capacity = DefaultCapacity,
            TimeSpan? maxAge = null,
            TimeSpan? delayBetweenSends = null,
            Func<DateTime>? utcNow = null,
            Action<string>? log = null)
        {
            _subscriberId = subscriberId;
            _maxAge = maxAge ?? DefaultMaxAge;
            _delayBetweenSends = delayBetweenSends ?? DefaultDelayBetweenSends;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _log = log ?? WriteError;

            _channel = Channel.CreateBounded<QueuedSend>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                },
                _ => Interlocked.Increment(ref _droppedSinceReport));

            Completion = Task.Run(RunAsync);
        }

        /// <summary>Finishes when <see cref="Complete"/> was called and every queued packet was handled.</summary>
        public Task Completion { get; }

        /// <summary>Packets waiting to be sent.</summary>
        public int Count => _channel.Reader.Count;

        /// <summary>
        /// Queues a send and returns at once. The send runs on this queue's worker; an exception it throws is
        /// logged and the next packet is sent.
        /// </summary>
        public void Enqueue(Func<Task> send)
        {
            _channel.Writer.TryWrite(new QueuedSend(_utcNow(), send));
        }

        /// <summary>Stops taking packets. Already queued ones are still handled.</summary>
        public void Complete() => _channel.Writer.TryComplete();

        private async Task RunAsync()
        {
            await foreach (var item in _channel.Reader.ReadAllAsync())
            {
                if (_utcNow() - item.QueuedAt > _maxAge)
                {
                    Interlocked.Increment(ref _droppedSinceReport);
                    ReportDrops();
                    continue;
                }

                try
                {
                    await item.Send();
                }
                catch (Exception ex)
                {
                    _log($"[{_subscriberId}] Error sending telemetry: {ex.GetType().Name}: {ex.Message}");
                }

                ReportDrops();

                if (_delayBetweenSends > TimeSpan.Zero)
                {
                    await Task.Delay(_delayBetweenSends);
                }
            }

            ReportDrops(force: true);
        }

        /// <summary>
        /// Logs how many packets were dropped since the last report: at most once a minute while the queue is
        /// backed up, and at once when it has emptied.
        /// </summary>
        private void ReportDrops(bool force = false)
        {
            var now = _utcNow();
            var due = force || _channel.Reader.Count == 0 || now - _lastDropReport >= DropReportInterval;
            if (!due)
            {
                return;
            }

            var dropped = Interlocked.Exchange(ref _droppedSinceReport, 0);
            if (dropped > 0)
            {
                _lastDropReport = now;
                _log($"[{_subscriberId}] Dropped {dropped} telemetry packet(s) that waited too long or did not fit in the queue; the API is not keeping up.");
            }
        }

        private static void WriteError(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{timestamp} {message}");
            Console.ResetColor();
        }

        private sealed record QueuedSend(DateTime QueuedAt, Func<Task> Send);
    }
}
