using NuGetValidators.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NuGetValidators.Artifact
{
    public static class ArtifactValidator
    {
        private const int _numberOfThreads = 1; // Currently this needs to be one else logs are mangled.
        private static IDictionary<string, Tuple<AuthentiCode.Result, string>> AuthentiCodeFailures = new Dictionary<string, Tuple<AuthentiCode.Result, string>>();
        private static IList<string> StrongNameFailures = new List<string>();
        private static object AuthentiCodeFailuresCollectionLock = new object();
        private static object StrongNameFailuresCollectionLock = new object();

        public static int ExecuteForVsix(string VsixPath, string VsixExtractPath, string OutputPath)
        {
            var vsixPath = VsixPath;
            var extractedVsixPath = VsixExtractPath;
            var logPath = OutputPath;

            VsixUtility.ExtractVsix(vsixPath, extractedVsixPath);

            var files = FileUtility.GetDlls(extractedVsixPath, isArtifacts: false);

            var result =  Execute(files);
            LogErrors(logPath);

            return result;
        }

        public static int ExecuteForArtifacts(string artifactsDirectory, string OutputPath)
        {
            var logPath = OutputPath;

            var files = FileUtility.GetDlls(artifactsDirectory, isArtifacts: true);

            var result = Execute(files);
            LogErrors(logPath);

            return result;
        }

        public static int ExecuteForFiles(IList<string> files, string OutputPath)
        {
            var logPath = OutputPath;

            var result = Execute(files.ToArray());
            LogErrors(logPath);

            return result;
        }

        // Types of validation - 
        //1. All files inside the artifacts location are strong name signed
        //2. All files inside the artifacts location are authenticode certified
        public static int Execute(string[] files)
        {
            Console.WriteLine($"Total files: {files.Count()}");
            var result = 0;

            ParallelOptions ops = new ParallelOptions { MaxDegreeOfParallelism = _numberOfThreads };
            Parallel.ForEach(files, ops, file =>
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine($"File Not Found: {file}");
                }
                else if (!file.EndsWith(".dll") && !file.EndsWith(".exe"))
                {
                    Console.WriteLine($"File Not a dll/exe: {file}\n");
                }
                else
                {
                    Console.WriteLine($"Validating: {file} ");
                    Console.WriteLine($"\t Verifying StongName...");
                    var strongNameVerificationResult = VerifyStrongName(file);
                    Console.WriteLine($"\t StongName Verification: {GetStringNameResultString(strongNameVerificationResult)}\n");

                    Console.WriteLine($"\t Verifying AuthentiCode...");
                    var authentiCodeVerificationResult = VerifyAuthentiCode(file);
                    Console.WriteLine($"\t AuthentiCode Verification: {AuthentiCode.GetResultString(authentiCodeVerificationResult)}\n");

                    if (strongNameVerificationResult != 0)
                    {
                        result = 1;
                        AddToStrongNameFailures(file);
                    }

                    if (authentiCodeVerificationResult != AuthentiCode.Result.Success)
                    {
                        var resultString = AuthentiCode.GetResultString(authentiCodeVerificationResult);

                        // If X509Certificate.Verify() failed then get a chain to get the reason for failure.
                        if (authentiCodeVerificationResult == AuthentiCode.Result.VerifyFailed)
                        {
                            var chain = new X509Chain();
                            var chainBuilt = chain.Build(AuthentiCode.GetCertificate(file));
                            if (chainBuilt == false)
                            {
                                var chainStatus = chain.ChainStatus.FirstOrDefault();
                                resultString = chainStatus.StatusInformation;
                            }
                            else
                            {
                                resultString = "X509Certificate.Verify() failed, but chain built fine.";
                            }
                        }

                        result = 1;
                        AddToAuthentiCodeFailures(file, authentiCodeVerificationResult, resultString);
                    }
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

        private static void AddToAuthentiCodeFailures(string file, AuthentiCode.Result result, string error)
        {
            lock (AuthentiCodeFailuresCollectionLock)
            {
                AuthentiCodeFailures[file] = new Tuple<AuthentiCode.Result, string>(result, error);
            }
        }

        private static void AddToStrongNameFailures(string file)
        {
            lock (StrongNameFailuresCollectionLock)
            {
                StrongNameFailures.Add(file);
            }
        }

        private static void LogErrors(string logPath)
        {
            if (!Directory.Exists(logPath))
            {
                Console.WriteLine($"INFO: Creating new Director for logs at '{logPath}'");
                Directory.CreateDirectory(logPath);
            }

            LogErrors(logPath,
                AuthentiCodeFailures,
                "AuthentiCode_Failures",
                "These Files had invalid/expired/none/wrong AuthentiCode certificate.");

            LogErrors(logPath,
                StrongNameFailures,
                "StrongName_Failures",
                "These Files had invalid/none StrongName.");
        }

        private static void LogErrors(string logPath, 
            IDictionary<string, Tuple<AuthentiCode.Result, string>> collection, 
            string logFileName, 
            string logDescription)
        {
            if (collection.Keys.Any())
            {
                var path = Path.Combine(logPath, logFileName + ".txt");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Error: {logDescription}");
                Console.WriteLine($"Failure count: {collection.Keys.Count}");
                Console.WriteLine($"Errors logged at: {path}");
                Console.WriteLine("================================================================================================================");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                using (StreamWriter w = File.AppendText(path))
                {
                    foreach (var file in collection.Keys)
                    {
                        var line = new StringBuilder();
                        line.Append($"File: {file}");
                        line.Append(Environment.NewLine);
                        line.Append($"Exit Code: {collection[file].Item1}");
                        line.Append(Environment.NewLine);
                        line.Append($"File: {collection[file].Item2}");
                        line.Append(Environment.NewLine);
                        line.Append(Environment.NewLine);

                        w.WriteLine(line.ToString());
                    }
                }
            }
            else
            {
                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Error: {logDescription}");
                Console.WriteLine($"Failure count: {collection.Count}");
                Console.WriteLine("================================================================================================================");
            }
        }

        private static void LogErrors(string logPath, 
            IList<string> collection,
            string logFileName,
            string logDescription)
        {
            if (collection.Any())
            {
                var path = Path.Combine(logPath, logFileName + ".txt");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Error: {logDescription}");
                Console.WriteLine($"Failure count: {collection.Count}");
                Console.WriteLine($"Errors logged at: {path}");
                Console.WriteLine("================================================================================================================");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                using (StreamWriter w = File.AppendText(path))
                {
                    foreach (var file in collection)
                    {
                        w.WriteLine($"File: {file}");
                    }
                }
            }
            else
            {
                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Error: {logDescription}");
                Console.WriteLine($"Failure count: {collection.Count}");
                Console.WriteLine("================================================================================================================");
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
