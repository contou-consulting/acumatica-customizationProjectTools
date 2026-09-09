using System;
using System.Collections.Concurrent;
using System.Management.Automation.Runspaces;
using System.Net.Http;

namespace AcuPackageTools.Connection
{
    /// <summary>
    /// Manages a shared connection to an Acumatica instance.
    /// Used by Connect-AcuInstance and API cmdlets.
    /// Connections are held per runspace: the module assembly is loaded once
    /// per process, so static state is visible to every runspace, and callers
    /// that deploy in parallel (one runspace per target) must not share or
    /// clobber each other's connection.
    /// </summary>
    public static class AcuConnectionManager
    {
        private sealed class ConnectionEntry
        {
            public HttpClient Client;
            public string Url;
            public string Tenant;
        }

        private static readonly ConcurrentDictionary<Guid, ConnectionEntry> _connections =
            new ConcurrentDictionary<Guid, ConnectionEntry>();

        // Cmdlets run on their runspace's pipeline thread, where the engine
        // sets DefaultRunspace. Off-runspace callers fall back to a single
        // shared slot, which matches the old global behavior.
        private static Guid CurrentKey => Runspace.DefaultRunspace?.InstanceId ?? Guid.Empty;

        /// <summary>
        /// Marker for callers that need to know connections are isolated per
        /// runspace (older versions held one static connection per process).
        /// </summary>
        public static bool ConnectionsArePerRunspace => true;

        public static bool IsConnected => _connections.ContainsKey(CurrentKey);

        public static string Url
            => _connections.TryGetValue(CurrentKey, out var entry) ? entry.Url : null;

        public static string Tenant
            => _connections.TryGetValue(CurrentKey, out var entry) ? entry.Tenant : null;

        public static HttpClient Client
            => _connections.TryGetValue(CurrentKey, out var entry) ? entry.Client : null;

        public static void SetConnection(HttpClient client, string url, string tenant)
        {
            _connections[CurrentKey] = new ConnectionEntry
            {
                Client = client,
                Url = url,
                Tenant = tenant
            };

            // A runspace that closes without Disconnect-AcuInstance would
            // otherwise leak its entry (and HttpClient) for the process
            // lifetime.
            Runspace runspace = Runspace.DefaultRunspace;
            if (runspace != null)
            {
                Guid key = runspace.InstanceId;
                EventHandler<RunspaceStateEventArgs> handler = null;
                handler = (sender, args) =>
                {
                    if (args.RunspaceStateInfo.State != RunspaceState.Closed
                     && args.RunspaceStateInfo.State != RunspaceState.Broken)
                        return;

                    runspace.StateChanged -= handler;
                    if (_connections.TryRemove(key, out var entry))
                        entry.Client?.Dispose();
                };
                runspace.StateChanged += handler;
            }
        }

        public static void ClearConnection()
        {
            if (_connections.TryRemove(CurrentKey, out var entry))
                entry.Client?.Dispose();
        }

        public static HttpClient CreateNewClient(bool skipCertificateCheck = false)
            => AcuClient.CreateHttpClient(skipCertificateCheck);
    }
}
