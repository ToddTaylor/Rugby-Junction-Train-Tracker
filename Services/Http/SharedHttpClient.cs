using System.Net;

namespace Services.Http
{
    /// <summary>
    /// The one HttpClient every subscriber uses. A client per request opens a new TCP and TLS connection each
    /// time and leaves sockets behind until the garbage collector runs, which a PC left running for months can
    /// exhaust. Pooled connections are renewed every few minutes so a DNS change still takes effect.
    /// </summary>
    public static class SharedHttpClient
    {
        /// <summary>
        /// Longest a single request may take. The default of 100 seconds let one unreachable API hold a packet
        /// for minutes; retries with backoff cover a server that is slow for a moment.
        /// </summary>
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        public static HttpClient Instance { get; } = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
        })
        {
            Timeout = RequestTimeout,
        };
    }
}
