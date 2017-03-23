using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetValidators
{
    class ArtifactValidator
    {
        private const int _numberOfThreads = 1;

        // Types of validation - 
        //1. All files inside the artifacts location are strong name signed
        //2. New vsix has the same content as a reference vsix

        public int ValidateSigning(string artifactsDirectory)
        {
            var result = 0;
            var files = Directory.GetFiles(artifactsDirectory, "*.*", SearchOption.AllDirectories);
            var snExePath = GetSnExePath();
            //ParallelOptions ops = new ParallelOptions { MaxDegreeOfParallelism = _numberOfThreads };
            //Parallel.ForEach(files, ops, file =>
            //{

            //});

            return result;
        }

        public int CompareVsix(string referenceVsix, string newVsix)
        {
            int result = 0;
            var tempDirectory = @"F:\validation\NuGetValidators.Artifact";
            //using (var tempDirectory = new TemporaryDirectory())
            {
                var referenceVsixDirectoryName = "ReferenceVsix";
                var newVsixDirectoryName = "NewVsix";
                var vsixName = "NuGet.Tools.vsix";

                var referenceVsixDirectory = Path.Combine(tempDirectory, referenceVsixDirectoryName, vsixName);
                var newVsixDirectory = Path.Combine(tempDirectory, newVsixDirectoryName, vsixName);

                //VsixUtility.ExtractVsix(referenceVsix, referenceVsixDirectory);
                //VsixUtility.ExtractVsix(newVsix, newVsixDirectory);

                var referenceFiles = Directory.GetFiles(referenceVsixDirectory, "*.*", SearchOption.AllDirectories);

                ParallelOptions ops = new ParallelOptions { MaxDegreeOfParallelism = _numberOfThreads };
                Parallel.ForEach(referenceFiles, ops, referenceFile =>
                {
                    var expectedFile = referenceFile.Replace(referenceVsixDirectoryName, newVsixDirectoryName);
                    ValidateFileExists(expectedFile);
                });
            }

            return result;
        }

        private bool ValidateFileExists(string expectedFile)
        {
            
            if (!File.Exists(expectedFile))
            {
                Console.WriteLine($"File '{Path.GetFileName(expectedFile)}' does not exist at expected path '{expectedFile}'");
                return false;
            }
            else
            {
                return true;
            }
        }

        private string GetSnExePath()
        {
            var sdkPath = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();

            var snExePath = Directory.GetFiles(sdkPath, "sn.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            return snExePath;
        }

    }
}
