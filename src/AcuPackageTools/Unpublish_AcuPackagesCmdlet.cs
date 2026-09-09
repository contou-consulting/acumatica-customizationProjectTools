using System.Management.Automation;
using AcuPackageTools.CmdletBase;
using AcuPackageTools.Models;

namespace AcuPackageTools
{
    [Cmdlet(VerbsData.Unpublish, "AcuPackages", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
    public class Unpublish_AcuPackagesCmdlet : ApiCmdlet
    {
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
        [Alias("tln")]
        public string[] TenantLoginNames { get; set; }

        public const string UnpublishAllEndpoint = "/CustomizationApi/unpublishAll";

        protected override void PerformApiOperations()
        {
            if (ShouldProcess(EffectiveUrl, "Unpublish all customization packages"))
            {
                RunPumped((ct, post) => AcuClient.UnpublishAllAsync(new UnpublishAllRequest(TenantMode, TenantLoginNames), ct));
            }
        }
    }
}
