using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGetStringChecker
{
    class Program
    {
        private static ConcurrentQueue<string> _nonLocalizedStringErrors = new ConcurrentQueue<string>();
        private static ConcurrentQueue<string> _missingLocalizedErrors = new ConcurrentQueue<string>();
        private static ConcurrentQueue<string> _misMatcherrors = new ConcurrentQueue<string>();
        private static ConcurrentQueue<string> _lockedStrings = new ConcurrentQueue<string>();
        private static Dictionary<string, Dictionary<string, List<string>>> _nonLocalizedStringErrorsDeduped = new Dictionary<string, Dictionary<string, List<string>>>();
        private static object _packageCollectionLock = new object();
        private static int _numberOfThreads = 8;

        public static void Main(string[] args)
        {
            if(args.Count() < 3)
            {
                Console.WriteLine("Please enter 3 arguments - ");
                Console.WriteLine("arg[0]: NuGet.Tools.Vsix path");
                Console.WriteLine("arg[1]: Path to extract NuGet.Tools.Vsix into. Folder need not be present, but Program should have write access to the location.");
                Console.WriteLine("arg[2]: Path to the directory for writing errors. File need not be present, but Program should have write access to the location.");
                Console.WriteLine("Exiting...");
                return;
            }

            var vsixPath = args[0];
            var extractedVsixPath = args[1];
            var logPath = args[2];

            //CleanExtractedFiles(extractedVsixPath);
            //ExtractVsix(vsixPath, extractedVsixPath);


            // For Testing
            //var vsixPath = @"\\wsr-tc\Drops\NuGet.Signed.AllLanguages\latest-successful\Signed\VSIX\15\NuGet.Tools.vsix";
            //var extractedVsixPath = @"\\nuget\NuGet\Share\ValidationTemp\NuGet.Tools.Vsix\";
            //var logPath = @"\\nuget\NuGet\Share\ValidationTemp";
            //var englishDlls = new string[] { @"\\nuget\NuGet\Share\ValidationTemp\NuGet.Tools.Vsix\NuGet.Options.dll" };

            var englishDlls = GetEnglishDlls(extractedVsixPath);
            var lciCommentsDirPath = @"E:\NuGet TFS\Main\localize\comments\15";


            ParallelOptions ops = new ParallelOptions { MaxDegreeOfParallelism = _numberOfThreads };
            Parallel.ForEach(englishDlls, ops, englishDll =>
            {
                Console.WriteLine($"Validating strings for ${englishDll}");
                var translatedDlls = GetTranslatedDlls(extractedVsixPath, Path.GetFileNameWithoutExtension(englishDll));

                foreach (var translatedDll in translatedDlls)
                {
                    try
                    {
                        CompareAllStrings(englishDll, translatedDll, lciCommentsDirPath);
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            });

            LogErrors(logPath);
            // Files are cleared at the begining of each run
            //CleanExtractedFiles(extractedVsixPath);
        }

        private static string[] GetEnglishDlls(string extractedVsixPath)
        {
            return Directory.GetFiles(extractedVsixPath, "NuGet.*.dll");
        }

        private static string[] GetTranslatedDlls(string extractedVsixPath, string englishDllName)
        {
            return Directory.GetFiles(extractedVsixPath, $"{englishDllName}.resources.dll", SearchOption.AllDirectories);
        }

        private static bool CompareAllStrings(string firstDll, string secondDll, string lciCommentDirPath)
        {
            var lciFilePath = Path.Combine(lciCommentDirPath, Path.GetFileName(secondDll) + ".lci");
            XElement lciFile = null;
            if (File.Exists(lciFilePath))
            {
                lciFile = XElement.Load(lciFilePath);
            }
            var result = true;
            var firstAssembly = Assembly.LoadFrom(firstDll);

            var firstAssemblyResources = firstAssembly
                .GetManifestResourceNames()
                .Where(r => r.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) && !r.EndsWith("g.resources", StringComparison.OrdinalIgnoreCase));

            var secondAssembly = Assembly.LoadFrom(secondDll);

            var secondAssemblyResources = secondAssembly
                .GetManifestResourceNames()
                .Where(r => r.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) && !r.EndsWith("g.resources", StringComparison.OrdinalIgnoreCase));

            foreach(var firstAssemblyResource in firstAssemblyResources)
            {

                var firstAssemblyResourceName = firstAssemblyResource
                    .Substring(0, firstAssemblyResource.LastIndexOf(".resource", StringComparison.OrdinalIgnoreCase));

                var secondAssemblyResource = secondAssemblyResources
                    .First(r => r.StartsWith(firstAssemblyResourceName));

                var secondAssemblyResourceName = secondAssemblyResource
                    .Substring(0, secondAssemblyResource.LastIndexOf(".resource", StringComparison.OrdinalIgnoreCase));


                var firstResourceManager = new ResourceManager(firstAssemblyResourceName, firstAssembly);
                var secondResourceManager = new ResourceManager(secondAssemblyResourceName, secondAssembly);

                var firstResourceSet = firstResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false);
                var secondResourceSet = secondResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false);

                var firstResourceSetEnumerator = firstResourceSet.GetEnumerator();

               while(firstResourceSetEnumerator.MoveNext())
                {
                    if ((firstResourceSetEnumerator.Key is string) && !((firstResourceSetEnumerator.Key.ToString()).StartsWith(">>")) && (firstResourceSetEnumerator.Value is string))
                    {

                        Uri uriResult;
                        if ((Uri.TryCreate((firstResourceSetEnumerator.Value as string), UriKind.Absolute, out uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp)) ||
                            (firstResourceSetEnumerator.Value as string).All(c => !char.IsLetter(c)))
                        {
                            continue;
                        }

                        var lciEntries = lciFile
                            ?.Descendants()
                            .Where(d => d.Name.LocalName.Equals("Item", StringComparison.OrdinalIgnoreCase))
                            .Where(d => d.Attribute(XName.Get("ItemId")).Value.Equals(";" + firstResourceSetEnumerator.Key, StringComparison.OrdinalIgnoreCase));

                        if (lciEntries?.Any() == true)
                        {
                            var lciEntry = lciEntries.First();
                            var valueData = lciEntry
                                .Descendants()
                                .Where(d => d.Name.LocalName.Equals("val", StringComparison.OrdinalIgnoreCase));
                            var valueString = ((XCData)valueData.First().FirstNode).Value;

                            var cmtData = lciEntry.Descendants()
                                .Where(d => d.Name.LocalName.Equals("cmt", StringComparison.OrdinalIgnoreCase));
                            var cmtString = ((XCData)cmtData.First().FirstNode).Value;

                            if (cmtString.Contains("Locked"))
                            {
                                var lockedString = $"Dll: {firstAssemblyResource}{Environment.NewLine}" +
                                        $"'{firstResourceSetEnumerator.Key}':'{firstResourceSetEnumerator.Value}' {Environment.NewLine}" +
                                        $"lcx:{cmtString}{Environment.NewLine}" +
                                       "================================================================================================================";
                                _lockedStrings.Enqueue(lockedString);
                            }

                            if (cmtString.Equals("{Locked}", StringComparison.OrdinalIgnoreCase) ||
                                cmtString.Equals("{Locked=\"" + valueString + "\"}", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        var secondResource = secondResourceSet.GetString(firstResourceSetEnumerator.Key as string);
                        if (secondResource == null)
                        {
                            var error = $"Resource '{firstResourceSetEnumerator.Key}' from english resource set '{firstAssemblyResource}' " +
                                $"MISSING in '{secondAssemblyResourceName}' in dll '{secondDll}'{Environment.NewLine}" +
                                $"'{firstResourceSetEnumerator.Key}':'{firstResourceSetEnumerator.Value}'{Environment.NewLine}" +
                                "================================================================================================================";
                            _missingLocalizedErrors.Enqueue(error);
                            result = false;
                        }
                        else if (!CompareStrings(firstResourceSetEnumerator.Value as string, secondResource))
                        {
                            var error = $"Resource '{firstResourceSetEnumerator.Key}' from english resource set '{firstAssemblyResource}' " +
                                $"NOT SAME as {secondAssemblyResourceName} in dll {secondDll}{Environment.NewLine}" +
                                $"'{firstResourceSetEnumerator.Key}':'{firstResourceSetEnumerator.Value}' {Environment.NewLine}" +
                                $"'{firstResourceSetEnumerator.Key}':'{secondResource}'{Environment.NewLine}" +
                                "================================================================================================================";
                            _misMatcherrors.Enqueue(error);
                            result = false;
                        }
                        else if (secondResource.Equals(firstResourceSetEnumerator.Value as string))
                        {
                            var error = $"Resource '{firstResourceSetEnumerator.Key}' from english resource set '{firstAssemblyResource}' " +
                                    $"EXACTLY SAME as {secondAssemblyResourceName} in dll {secondDll}{Environment.NewLine}" +
                                    $"'{firstResourceSetEnumerator.Key}':'{firstResourceSetEnumerator.Value}' {Environment.NewLine}" +
                                    $"'{firstResourceSetEnumerator.Key}':'{secondResource}'{Environment.NewLine}" +
                                    "================================================================================================================";
                            _nonLocalizedStringErrors.Enqueue(error);
                            AddToCollection(_nonLocalizedStringErrorsDeduped,
                                Path.GetFileName(firstDll),
                                firstResourceSetEnumerator.Key as string,
                                Directory.GetParent(secondDll).Name);

                            result = false;
                        }
                    }
                }
            }

            return result;
        }

        private static bool CompareStrings(string firstString, string secondString)
        {
            var firstStringMetadata = GetStringMetadata(firstString);
            var secondStringMetadata = GetStringMetadata(secondString);
            return StringMetadataEquals(firstStringMetadata, secondStringMetadata);
        }

        private static Dictionary<string, int> GetStringMetadata(string str)
        {
            var result = new Dictionary<string, int>();
            var current = new StringBuilder();
            var i = 0;
            while (i < str.Length-1)
            {
                if(str[i] == '{' && str[i+1] == '{')
                {
                    i += 2;
                }
                if (str[i] == '{' && str[i+1] != '{')
                {
                    var closingIndex = str.IndexOf('}', i);
                    if (closingIndex == -1)
                    {
                        var pacleHolderString = str.Substring(i);
                        AddResult(result, pacleHolderString);

                        return result;
                    }
                    else if(closingIndex < str.Length-1 && str[closingIndex+1] == '}')
                    {
                        i += 2;
                    }
                    else
                    {
                        var pacleHolderString = str.Substring(i, closingIndex - i + 1);
                        AddResult(result, pacleHolderString);

                        i = closingIndex + 1;
                    }

                }
                else
                {
                    i += 1;
                }
            }
            return result;
        }

        private static void AddResult(Dictionary<string, int> result, string placeHolderString)
        {
            if (result.ContainsKey(placeHolderString))
            {
                result[placeHolderString]++;
            }
            else
            {
                result.Add(placeHolderString, 0);
            }
        }

        private static bool StringMetadataEquals(Dictionary<string, int> firstMetadata, Dictionary<string, int> secondMetadata)
        {
            var unequalMetadata = firstMetadata
                .Where(entry => !secondMetadata.ContainsKey(entry.Key) || secondMetadata[entry.Key] != entry.Value);
            return unequalMetadata.Count() == 0;
        }
        
        private static void AddToCollection(Dictionary<string, Dictionary<string, List<string>>> collection, 
            string dllName, string resourceName, string language)
        {
            lock (_packageCollectionLock)
            {
                if (collection.ContainsKey(dllName))
                {
                    if (collection[dllName].ContainsKey(resourceName))
                    {
                        collection[dllName][resourceName].Add(language.ToLower());
                    }
                    else
                    {
                        collection[dllName][resourceName] = new List<string> { language.ToLower() };
                    }
                }
                else
                {
                    collection[dllName] = new Dictionary<string, List<string>> { { resourceName, new List<string> { language.ToLower() } } };
                }
            }
        }

        private static void LogErrors(string logPath)
        {
            LogErrors(logPath, 
                _nonLocalizedStringErrors, 
                "Not_Localized_Strings", 
                "These Strings are same as English strings");

            LogErrors(logPath, 
                _misMatcherrors, 
                "Mismatch_Strings", 
                "These Strings do not contain the same number of place holders as the English strings");

            LogErrors(logPath, 
                _missingLocalizedErrors, 
                "Missing_Strings", 
                "These Strings are missing in the localized resources");

            LogErrors(logPath,
                _lockedStrings,
                "Locked_Strings",
                "These Strings are missing in the localized resources");

            LogCollectionToXslt(logPath,
                _nonLocalizedStringErrorsDeduped,
                "Not_Localized_Strings_Deduped",
                "These Strings are same as English strings");
        }

        private static void LogErrors(string logPath, 
            ConcurrentQueue<string>errors, 
            string errorType, 
            string errorDescription)
        {
            var path = Path.Combine(logPath, errorType + ".txt");

            Console.WriteLine("================================================================================================================");
            Console.WriteLine($"Error Type: {errorType} - {errorDescription}");
            Console.WriteLine($"Total error count: {errors.Count()}");
            Console.WriteLine($"Errors logged at: {path}");
            Console.WriteLine("================================================================================================================");

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using (StreamWriter w = File.AppendText(path))
            {
                foreach(var error in errors)
                {
                    w.WriteLine(error);
                }
            }
        }

        private static void CleanExtractedFiles(string path)
        {
            Console.WriteLine("Cleaning up the extracted files");
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static void ExtractVsix(string vsixPath, string extractedVsixPath)
        {
            Console.WriteLine($"Extracting {vsixPath} to {extractedVsixPath}");

            ZipFile.ExtractToDirectory(vsixPath, extractedVsixPath);

            Console.WriteLine($"Done Extracting...");
        }

        private static void LogCollectionToXslt(string logPath, 
            Dictionary<string, Dictionary<string, List<string>>> collection, 
            string logFileName, 
            string logDescription)
        {
            var path = Path.Combine(logPath, logFileName + ".csv");

            Console.WriteLine("================================================================================================================");
            Console.WriteLine($"Error Type: {logFileName} - {logDescription}");
            Console.WriteLine($"Errors logged at: {path}");
            Console.WriteLine("================================================================================================================");

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            using (StreamWriter w = File.AppendText(path))
            {
                w.WriteLine("Dll Name, Resource Name, cs, de, es, fr, it, ja, ko, pl, pt-br, ru, tr, zh-hans, zh-hant");
                foreach (var dll in collection.Keys)
                {
                    foreach (var resource in collection[dll].Keys)
                    {
                        var line = new StringBuilder();
                        line.Append(dll);
                        line.Append(",");
                        line.Append(resource);
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("cs") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("de") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("es") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("fr") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("it") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("ja") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("ko") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("pl") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("pt-br") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("ru") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("tr") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("zh-hans") ? "Error" : "");
                        line.Append(",");
                        line.Append(collection[dll][resource].Contains("zh-hant") ? "Error" : "");
                        line.Append(",");

                        w.WriteLine(line.ToString());
                    }
                }
            }
        }
        private static void LogCollectionToXsltSimple(string logPath,
            Dictionary<string, Dictionary<string, List<string>>> collection,
            string logFileName,
            string logDescription)
        {
            var path = Path.Combine(logPath, logFileName + "_Simple.csv");

            Console.WriteLine("================================================================================================================");
            Console.WriteLine($"Error Type: {logFileName} - {logDescription}");
            Console.WriteLine($"Errors logged at: {path}");
            Console.WriteLine("================================================================================================================");

            if (File.Exists(path))
            {
                File.Delete(path);
            }
            using (StreamWriter w = File.AppendText(path))
            {
                foreach (var dll in collection.Keys)
                {
                    foreach (var resource in collection[dll].Keys)
                    {
                        foreach (var langauge in collection[dll][resource])
                        {
                            w.WriteLine(string.Concat(dll, ",", resource, ",", langauge));
                        }
                    }
                }
            }
        }
    }
}
