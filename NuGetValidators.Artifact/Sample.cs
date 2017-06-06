using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetValidators.Artifact
{
    class Sample
    {
        public static void Main(string[] args)
        {
            var devVsixPath = @"E:\nuget.client\artifacts\VS14\NuGet.Tools.vsix";
            var migratedVsixPath = @"E:\nuget.client\artifacts\VS15\NuGet.Tools.vsix";
            var artifactValidator = new ArtifactValidator();
            //artifactValidator.ValidateSigning(@"C:\Users\anmishr\Desktop\Temp\EndToEnd");
            artifactValidator.CompareVsix(devVsixPath, migratedVsixPath);
        }
    }
}
