using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var files = Directory.GetFiles(artifactsDirectory, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith("dll") )
                .ToArray();
            var snExePath = @"C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe";
            //var snExePath = GetSnExePath();

            ParallelOptions ops = new ParallelOptions { MaxDegreeOfParallelism = _numberOfThreads };
            Parallel.ForEach(files, ops, file =>
            {
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = snExePath,
                    Arguments = $" -v {file}"
                };
                //Console.WriteLine("======================================================");
                //Console.WriteLine($"{snExePath} -v {file}");
                using (var process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {

                        Console.WriteLine($"Error in file '{file}'");

                    }
                }
                //Console.WriteLine("======================================================");
            });

            return result;
        }

        public int CompareVsix(string referenceVsix, string newVsix)
        {
            int result = 0;
            Console.WriteLine("==========================================================");
            using (var tempDirectory = new TemporaryDirectory())
            {
                var referenceVsixDirectoryName = "ReferenceVsix";
                var newVsixDirectoryName = "NewVsix";
                var vsixName = "NuGet.Tools.vsix";

                var referenceVsixDirectory = Path.Combine(tempDirectory, referenceVsixDirectoryName, vsixName);
                var newVsixDirectory = Path.Combine(tempDirectory, newVsixDirectoryName, vsixName);

                VsixUtility.ExtractVsix(referenceVsix, referenceVsixDirectory);
                VsixUtility.ExtractVsix(newVsix, newVsixDirectory);

                var referenceFiles = Directory.GetFiles(referenceVsixDirectory, "*.*", SearchOption.AllDirectories);

                ParallelOptions ops = new ParallelOptions { MaxDegreeOfParallelism = _numberOfThreads };
                Parallel.ForEach(referenceFiles, ops, referenceFile =>
                {
                    var expectedFile = referenceFile.Replace(referenceVsixDirectoryName, newVsixDirectoryName);
                    ValidateFileExists(expectedFile);
                });
            }
            Console.WriteLine("==========================================================");
            return result;
        }

        private bool ValidateFileExists(string expectedFile)
        {
            
            if (!File.Exists(expectedFile))
            {
                Console.WriteLine($"ERROR: File '{Path.GetFileName(expectedFile)}' does not exist at expected path '{expectedFile}'");
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
