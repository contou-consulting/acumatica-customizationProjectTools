using System.Text;
using System.Text.Json;
using System;
using System.Management.Automation;
using System.Net.Http;
using AcuPackageTools.Connection;
using AcuPackageTools.Models;
using AcuPackageTools.CmdletBase;

namespace AcuPackageTools
{
    [Cmdlet(VerbsCommunications.Connect, "AcuInstance")]
    public class Connect_AcuInstanceCmdlet : PSCmdlet
    {
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Url { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Credential]
        [ValidateNotNull]
        public PSCredential Credential { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("t")]
        public string Tenant { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter SkipCertificateCheck { get; set; }

        protected override void ProcessRecord()
        {
            if (AcuConnectionManager.IsConnected)
            {
                WriteWarning($"Already connected to {AcuConnectionManager.Url}. Use Disconnect-AcuInstance first.");
                return;
            }

            try
            {
                if (SkipCertificateCheck.IsPresent)
                {
                    WriteVerbose($"Skipping TLS certificate validation for {Url}");
                }

                var client = AcuConnectionManager.CreateNewClient(SkipCertificateCheck.IsPresent);
                var networkCredential = Credential.GetNetworkCredential();
                var loginRequest = new LoginRequest(
                    networkCredential.UserName,
                    networkCredential.Password,
                    Tenant);

                var uriBuilder = new UriBuilder(Url);
                uriBuilder.Path += "/entity/auth/login";
                var loginUrl = uriBuilder.ToString();

                var requestContent = JsonSerializer.Serialize(loginRequest, ApiCmdlet.SerializerOptions);

                WriteVerbose($"Connecting to {Url}...");

                var response = client.PostAsync(loginUrl,
                    new StringContent(requestContent, Encoding.UTF8, "application/json"))
                    .GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    client.Dispose();
                    throw new HttpRequestException(
                        $"Failed to connect to {Url} (HTTP {(int)response.StatusCode}): {errorContent}");
                }

                AcuConnectionManager.SetConnection(client, Url, Tenant);
                WriteVerbose($"Successfully connected to {Url}");

                // Output connection info
                WriteObject(new {
                    Connected = true,
                    Url = Url,
                    Tenant = Tenant ?? "(default)",
                    User = networkCredential.UserName
                });
            }
            catch (Exception e)
            {
                WriteError(new ErrorRecord(e, "AcuConnectionFailed", ErrorCategory.ConnectionError, Url));
            }
        }
    }
}
