using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AcuPackageTools.Connection;

namespace AcuPackageTools.CmdletBase
{
    public abstract class ApiCmdlet : PSCmdlet, IDisposable
    {
        protected HttpClient Client;
        private bool _disposed;
        private bool _loggedIn;
        private bool _useSharedConnection;
        private string _effectiveUrl;
        private CancellationTokenSource _cts;

        // Kept in sync with AcuClient.SerializerOptions in AcuPackageTools.Core;
        // still used directly by Connect-AcuInstance.
        internal static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Url { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Credential]
        public PSCredential Credential { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("t")]
        public string Tenant { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter SkipCertificateCheck { get; set; }

        protected AcuClient AcuClient { get; private set; }

        protected override void BeginProcessing()
        {
            // Determine which mode to use
            if (Credential != null && !string.IsNullOrEmpty(Url))
            {
                // One-off mode: credentials provided explicitly
                _useSharedConnection = false;
                _effectiveUrl = Url;
                Client = AcuConnectionManager.CreateNewClient(SkipCertificateCheck.IsPresent);
                AcuClient = new AcuClient(Client, new AcuClientOptions { Url = _effectiveUrl, Tenant = Tenant });
                WriteVerbose("Using one-off connection mode");
            }
            else if (AcuConnectionManager.IsConnected)
            {
                // Shared connection mode
                _useSharedConnection = true;
                _effectiveUrl = AcuConnectionManager.Url;
                Client = AcuConnectionManager.Client;
                AcuClient = new AcuClient(Client, new AcuClientOptions { Url = _effectiveUrl, Tenant = AcuConnectionManager.Tenant });
                WriteVerbose($"Using shared connection to {_effectiveUrl}");
            }
            else
            {
                // No connection available
                throw new InvalidOperationException(
                    "Not connected to an Acumatica instance. " +
                    "Use Connect-AcuInstance first, or provide -Url and -Credential parameters.");
            }
        }

        protected override void ProcessRecord()
        {
            try
            {
                if (!_useSharedConnection)
                {
                    Login();
                }
                PerformApiOperations();
            }
            catch (Exception e)
            {
                var category = e is HttpRequestException
                    ? ErrorCategory.ConnectionError
                    : ErrorCategory.NotSpecified;
                WriteError(new ErrorRecord(e, "AcuApiRequestFailed", category, _effectiveUrl));
            }
            finally
            {
                if (!_useSharedConnection)
                {
                    Logout();
                }
            }
        }

        protected abstract void PerformApiOperations();

        protected override void EndProcessing()
        {
            if (!_useSharedConnection)
            {
                Dispose();
            }
        }

        private void Login()
        {
            var networkCredential = Credential.GetNetworkCredential();
            var loginRequest = new AcuPackageTools.Models.LoginRequest(
                networkCredential.UserName,
                networkCredential.Password,
                Tenant);
            SendRequest("/entity/auth/login", loginRequest);
            _loggedIn = true;
        }

        protected string EffectiveUrl => _effectiveUrl;

        protected JsonDocument SendRequest(string resource, object body = null)
        {
            return RunPumped((ct, post) => AcuClient.SendRequestAsync(resource, body, ct));
        }

        /// <summary>
        /// Runs an async Core operation while pumping its callbacks onto the
        /// pipeline thread, where the Write* methods are legal. Core callbacks
        /// (and the <c>post</c> delegate handed to <paramref name="operation"/>)
        /// enqueue write actions; this thread drains the queue in FIFO order
        /// until the task completes, so the observed stream ordering matches the
        /// old synchronous implementation, including messages queued before a
        /// failure. The task's exception is rethrown unwrapped.
        /// </summary>
        protected T RunPumped<T>(Func<CancellationToken, Action<Action>, Task<T>> operation)
        {
            _cts ??= new CancellationTokenSource();
            using var events = new BlockingCollection<Action>(new ConcurrentQueue<Action>());

            void Post(Action write)
            {
                try
                {
                    events.Add(write);
                }
                catch (InvalidOperationException) { } // completed or disposed: drop the message
            }

            AcuClient.Verbose = message => Post(() => WriteVerbose(message));
            try
            {
                Task<T> task = operation(_cts.Token, Post);
                task.ContinueWith(
                    _ =>
                    {
                        try
                        {
                            events.CompleteAdding();
                        }
                        catch (ObjectDisposedException) { }
                    },
                    TaskContinuationOptions.ExecuteSynchronously);

                try
                {
                    foreach (Action write in events.GetConsumingEnumerable())
                    {
                        write();
                    }
                }
                catch
                {
                    // A Write* threw (e.g. PipelineStoppedException on Ctrl+C).
                    // Observe the orphaned task's eventual fault so it never
                    // surfaces as an unobserved task exception.
                    task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.ExecuteSynchronously);
                    throw;
                }

                return task.GetAwaiter().GetResult();
            }
            finally
            {
                AcuClient.Verbose = null;
            }
        }

        protected void RunPumped(Func<CancellationToken, Action<Action>, Task> operation)
        {
            RunPumped(async (ct, post) =>
            {
                await operation(ct, post).ConfigureAwait(false);
                return true;
            });
        }

        private void Logout()
        {
            if (!_loggedIn) return;
            try
            {
                SendRequest("/entity/auth/logout");
            }
            catch (Exception e)
            {
                WriteWarning($"Error during logout: {e.Message}");
            }
            finally
            {
                _loggedIn = false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing && !_useSharedConnection)
            {
                Client?.Dispose();
            }
            _disposed = true;
        }

        protected override void StopProcessing()
        {
            _cts?.Cancel();
            if (!_useSharedConnection)
            {
                Dispose();
            }
            base.StopProcessing();
        }
    }
}
