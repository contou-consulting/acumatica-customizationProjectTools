using System.Management.Automation;
using AcuPackageTools.Helper;

namespace AcuPackageTools
{
    [Cmdlet(VerbsCommon.New, "AcuPackage")]
    public class New_AcuPackageCmdlet : PSCmdlet
    {
        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("cp")]
        [ValidateNotNullOrEmpty]
        public string CustomizationPath { get; set; }

        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("pfn")]
        [ValidateNotNullOrEmpty]
        public string PackageFileName { get; set; }

        [Parameter(
            Mandatory = true,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("v")]
        [ValidateNotNullOrEmpty]
        [ValidatePattern(@"^\d+\.\d+$", Options = System.Text.RegularExpressions.RegexOptions.None)]
        public string ProductVersion { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("d")]
        public string Description { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        [Alias("l")]
        public int? Level { get; set; }

        protected override void ProcessRecord()
        {
            WriteVerbose($"Building Package {PackageFileName}");

            PackageBuilder.BuildCustomizationPackage(
                CustomizationPath,
                PackageFileName,
                Description,
                Level,
                ProductVersion,
                WriteVerbose);

            WriteVerbose($"Package {PackageFileName} Completed Build");
        }
    }
}
