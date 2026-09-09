using AcuPackageTools.CmdletBase;
using System;
using System.IO;
using System.Management.Automation;
using AcuPackageTools.Models;

namespace AcuPackageTools
{
    [Cmdlet(VerbsData.Import, "AcuPackage")]
    public class Import_AcuPackageCmdlet : ApiCmdlet
    {
        public const string ImportEndpoint = "/CustomizationApi/import";

        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("pn")]
        [ValidateNotNullOrEmpty]
        public string PackageName { get; set; }

        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("pp")]
        [ValidateNotNullOrEmpty]
        public string PackagePath { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("r")]
        public SwitchParameter ReplacePackage { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("d")]
        public string PackageDescr { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("lvl")]
        public int? Level { get; set; }

        protected override void PerformApiOperations()
        {
            if (!File.Exists(PackagePath))
            {
                throw new InvalidOperationException("File does not exist at path: " + PackagePath);
            }

            var fileBytes = File.ReadAllBytes(PackagePath);
            var request =
                new ImportPackageRequest(Level,
                    ReplacePackage,
                    PackageName, PackageDescr,
                    Convert.ToBase64String(fileBytes));

            var responseObject = RunPumped((ct, post) => AcuClient.ImportPackageAsync(request, ct));

            foreach (var log in responseObject.Log)
            {
                WriteVerbose(log.Message);
            }
        }
    }
}
