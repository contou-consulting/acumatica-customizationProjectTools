using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace AcuPackageTools.Connection
{
    /// <summary>
    /// Manages a shared connection to an Acumatica instance.
    /// Used by Connect-AcuInstance and API cmdlets.
    /// </summary>
    public static class AcuConnectionManager
    {
        private static HttpClient _client;
        private static string _url;
        private static string _tenant;
        private static bool _isConnected;
        private static readonly object _lock = new object();

        public static bool IsConnected
        {
            get { lock (_lock) { return _isConnected; } }
        }

        public static string Url
        {
            get { lock (_lock) { return _url; } }
        }

        public static string Tenant
        {
            get { lock (_lock) { return _tenant; } }
        }

        public static HttpClient Client
        {
            get { lock (_lock) { return _client; } }
        }

        public static void SetConnection(HttpClient client, string url, string tenant)
        {
            lock (_lock)
            {
                _client = client;
                _url = url;
                _tenant = tenant;
                _isConnected = true;
            }
        }

        public static void ClearConnection()
        {
            lock (_lock)
            {
                _client?.Dispose();
                _client = null;
                _url = null;
                _tenant = null;
                _isConnected = false;
            }
        }

        public static HttpClient CreateNewClient(bool skipCertificateCheck = false)
        {
            var handler = new HttpClientHandler()
            {
                CookieContainer = new CookieContainer()
            };
            if (skipCertificateCheck)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }
            var client = new HttpClient(handler, true);
            // Publish requests (publishBegin) can legitimately run longer than the
            // 100-second HttpClient default; completion is tracked by polling publishEnd.
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
