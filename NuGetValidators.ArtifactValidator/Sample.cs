using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetValidators
{
    class Sample
    {
        public static void Main(string[] args)
        {
            var devVsixPath = @"\\nuget\NuGet\Share\Drops\CI\NuGet.Client\dev\2421\artifacts\VS15\Insertable\NuGet.Tools.vsix";
            var migratedVsixPath = @"\\nuget\NuGet\Share\Drops\CI\NuGet.Client\dev-anmishr-migrate2\17\artifacts\VS15\Insertable\NuGet.Tools.vsix";
            var artifactValidator = new ArtifactValidator();
            //artifactValidator.ValidateSigning(@"E:\migrate\NuGet.Client\artifacts");
            artifactValidator.CompareVsix(devVsixPath, migratedVsixPath);
        }
    }
}
