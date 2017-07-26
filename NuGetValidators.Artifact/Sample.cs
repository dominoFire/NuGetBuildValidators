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
            var cert = AuthentiCode.GetCertificate(@"F:\validation\NuGetValidators.Artifact\extracted\NuGet.Commands.dll");
            AuthentiCode.Verify(cert, displayCertMetadata: true);
        }
    }
}
