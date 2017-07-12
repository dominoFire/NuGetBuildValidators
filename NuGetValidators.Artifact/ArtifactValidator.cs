using NuGetValidators.Utility;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NuGetValidators.Artifact
{
    public static class ArtifactValidator
    {
        private const int _numberOfThreads = 1;

        // Types of validation - 
        //1. All files inside the artifacts location are strong name signed
        //2. New vsix has the same content as a reference vsix

        public static int ExecuteForVsix(string VsixPath, string VsixExtractPath, string OutputPath)
        {
            var vsixPath = VsixPath;
            var extractedVsixPath = VsixExtractPath;
            var logPath = OutputPath;

            VsixUtility.CleanExtractedFiles(extractedVsixPath);
            VsixUtility.ExtractVsix(vsixPath, extractedVsixPath);

            var files = FileUtility.GetDlls(extractedVsixPath, isArtifacts: false);
            return Execute(files);
        }

        public static int ExecuteForArtifacts(string artifactsDirectory)
        {
            var files = FileUtility.GetDlls(artifactsDirectory, isArtifacts: true);
            return Execute(files);
        }

        public static int Execute(string[] files)
        {
            var result = 0;
            

            ParallelOptions ops = new ParallelOptions { MaxDegreeOfParallelism = _numberOfThreads };
            Parallel.ForEach(files, ops, file =>
            {                
                Console.WriteLine($"Validating: {file} ");
                Console.WriteLine($"\t Verifying StongName...");
                var strongNameVerificationResult = VerifyStrongName(file);
                Console.WriteLine($"\t StongName Verification: {GetStringNameResultString(strongNameVerificationResult)}\n");

                Console.WriteLine($"\t Verifying AuthentiCode...");
                var authentiCodeVerificationResult = VerifyAuthentiCode(file);
                Console.WriteLine($"\t AuthentiCode Verification: {AuthentiCode.GetResultString(authentiCodeVerificationResult)}\n");

                if (strongNameVerificationResult != 0 || authentiCodeVerificationResult != AuthentiCode.Result.Success)
                {
                    result = 1;
                }

            });

            return result;
        }

        private static int VerifyStrongName(string file)
        {
            var result = 0;
            var snExePath = GetSnExePath();

            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = snExePath,
                Arguments = $" -v {file}",
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    result = 1;
                    Console.WriteLine($"\t\t {snExePath} -v {file}");

                    foreach (var line in process.StandardOutput.ReadToEnd()?.Split('\n'))
                    {
                        if (!(string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line)))
                        {
                            Console.WriteLine($"\t\t {line}");
                        }
                    }

                    foreach (var line in process.StandardError.ReadToEnd()?.Split('\n'))
                    {
                        if (!(string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line)))
                        {
                            Console.WriteLine($"\t\t {line}");
                        }
                    }
                    Console.WriteLine($"\t\t Exit Code: {process.ExitCode}");
                }
            }

            return result;
        }

        private static AuthentiCode.Result VerifyAuthentiCode(string file)
        {
            var result = AuthentiCode.Verify(file, displayCertMetadata: false);
            return result;
        }

        public static int CompareVsix(string referenceVsix, string newVsix)
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

        private static bool ValidateFileExists(string expectedFile)
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

        private static string GetSnExePath()
        {
            return @"sn.exe";
        }

        private static string GetStringNameResultString(int result)
        {
            if (result == 0)
            {
                return "SUCCESS - The StrongName was successfully verified.";
            }
            else
            {
                return "FAILED - Invalid StrongName.";
            }
        }
    }
}
