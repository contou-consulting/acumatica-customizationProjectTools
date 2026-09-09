using System;
using System.IO;
using System.Management.Automation;
using AcuPackageTools.CmdletBase;
using AcuPackageTools.Models;

namespace AcuPackageTools
{
    [Cmdlet(VerbsData.Export, "AcuPackage")]
    public class Export_AcuPackageCmdlet : ApiCmdlet
    {
        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("pn")]
        [ValidateNotNullOrEmpty]
        public string ProjectName { get; set; }

        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("o", "pp")]
        [ValidateNotNullOrEmpty]
        public string OutputPath { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public SwitchParameter AutoResolveConflicts { get; set; }

        public const string GetProjectEndpoint = "/CustomizationApi/getProject";

        protected override void PerformApiOperations()
        {
            WriteVerbose($"Exporting project '{ProjectName}' from {EffectiveUrl}");

            var responseObject = RunPumped((ct, post) => AcuClient.GetProjectAsync(
                new GetProjectRequest(ProjectName, AutoResolveConflicts.IsPresent), ct));

            if (responseObject.HasConflicts)
            {
                WriteWarning("The project has file system conflicts. " +
                    (AutoResolveConflicts.IsPresent
                        ? "Changes from file system were included."
                        : "Use -AutoResolveConflicts to include file system changes."));
            }

            foreach (var log in responseObject.Log)
            {
                WriteVerbose(log.Message);
            }

            if (string.IsNullOrEmpty(responseObject.ProjectContentBase64))
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException("No project content returned from API"),
                    "AcuExportEmpty",
                    ErrorCategory.InvalidResult,
                    ProjectName));
                return;
            }

            var packageBytes = Convert.FromBase64String(responseObject.ProjectContentBase64);
            File.WriteAllBytes(OutputPath, packageBytes);

            WriteVerbose($"Package saved to {OutputPath} ({packageBytes.Length} bytes)");
        }
    }
}
