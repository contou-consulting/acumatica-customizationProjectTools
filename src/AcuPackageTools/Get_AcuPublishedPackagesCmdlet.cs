using System.Management.Automation;
using AcuPackageTools.CmdletBase;

namespace AcuPackageTools
{
    [Cmdlet(VerbsCommon.Get, "AcuPublishedPackages")]
    public class Get_AcuPublishedPackagesCmdlet : ApiCmdlet
    {
        public const string GetPublishedEndpoint = "/CustomizationApi/getPublished";

        protected override void PerformApiOperations()
        {
            var responseObject = RunPumped((ct, post) => AcuClient.GetPublishedAsync(ct));

            foreach (var log in responseObject.Log)
            {
                WriteVerbose(log.Message);
            }

            WriteObject(responseObject);
        }
    }
}
