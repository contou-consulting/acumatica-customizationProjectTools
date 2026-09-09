using System.Management.Automation;
using System.Net.Http;
using AcuPackageTools.CmdletBase;
using AcuPackageTools.Models;

namespace AcuPackageTools
{
    [Cmdlet(VerbsData.Publish, "AcuPackage", SupportsShouldProcess = true)]
    public class Publish_AcuPackageCmdlet : ApiCmdlet
    {
        [Alias("pn")]
        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string[] ProjectNames { get; set; }

        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("tm")]
        public TenantMode TenantMode { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public SwitchParameter MergeWithExisting { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public SwitchParameter OnlyValidate { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public SwitchParameter DbUpdateOnly { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public SwitchParameter ExecuteAllScripts { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("tln")]
        public string[] TenantLoginNames { get; set; }


        public const string PublishBeginEndpoint = "/CustomizationApi/publishBegin";
        public const string PublishEndEndpoint = "/CustomizationApi/publishEnd";

        protected override void PerformApiOperations()
        {
            var projectList = string.Join(", ", ProjectNames);
            if (!ShouldProcess(projectList, "Publish customization packages"))
            {
                return;
            }

            var request = new PublishBeginRequest(
                MergeWithExisting,
                OnlyValidate,
                DbUpdateOnly,
                ExecuteAllScripts,
                ProjectNames,
                TenantMode,
                TenantLoginNames);

            var progressRecord = new ProgressRecord(1, "Publishing Packages", "Starting publication...");

            var responseData = RunPumped((ct, post) => AcuClient.PublishAsync(
                request,
                onLog: log => post(() =>
                {
                    switch (log.LogType)
                    {
                        case "information":
                            WriteInformation(new InformationRecord(log.Message, "Customization API"));
                            break;
                        case "warning":
                            WriteWarning(log.Message);
                            break;
                        case "error":
                            WriteWarning(log.Message);
                            break;
                    }
                }),
                onPollTick: elapsedSeconds => post(() =>
                {
                    progressRecord.StatusDescription = $"Waiting for completion... ({elapsedSeconds}s)";
                    WriteProgress(progressRecord);
                }),
                cancellationToken: ct));

            if (responseData.IsFailed)
                WriteError(
                    new ErrorRecord(
                        new HttpRequestException("Customization publish failed. Check the log output for details."),
                        "AcuPublishFailed",
                        ErrorCategory.NotSpecified,
                        ProjectNames));

            progressRecord.RecordType = ProgressRecordType.Completed;
            WriteProgress(progressRecord);
        }
    }
}
