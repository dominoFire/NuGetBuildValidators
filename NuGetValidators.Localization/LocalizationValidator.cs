using NuGetValidators.Utility;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NuGetValidators.Localization
{
    public class LocalizationValidator
    {
        private static ConcurrentQueue<string> _nonLocalizedStringErrors = new ConcurrentQueue<string>();
        private static ConcurrentQueue<string> _missingLocalizedErrors = new ConcurrentQueue<string>();
        private static ConcurrentQueue<string> _misMatcherrors = new ConcurrentQueue<string>();
        private static ConcurrentQueue<string> _lockedStrings = new ConcurrentQueue<string>();
        private static Dictionary<string, Dictionary<string, List<string>>> _nonLocalizedStringErrorsDeduped = new Dictionary<string, Dictionary<string, List<string>>>();
        private static Dictionary<string, List<string>> _localizedDlls = new Dictionary<string, List<string>>();
        private static object _packageStringCollectionLock = new object();
        private static object _packageDllCollectionLock = new object();

        private static int _numberOfThreads = 8;
        private static HashSet<string> _languages = new HashSet<string> { "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-br", "ru", "tr", "zh-hans", "zh-hant" };

        public static int Main(string[] args)
        {
            if(args.Count() < 4)
            {
                Console.WriteLine("Please enter the following 4 arguments - ");
                Console.WriteLine("arg[0]: NuGet.Tools.Vsix path");
                Console.WriteLine("arg[1]: Path to extract NuGet.Tools.Vsix into. Folder need not be present, but Program should have write access to the location.");
                Console.WriteLine("arg[2]: Path to the directory for writing errors. File need not be present, but Program should have write access to the location.");
                Console.WriteLine("arg[3]: Path to the local NuGet localization repository. e.g. - <repo_root>\\Main\\localize\\comments\\15");
                Console.WriteLine("Exiting...");
                return 1;
            }

            return ExecuteForVsix(VsixPath: args[0], VsixExtractPath: args[1], OutputPath: args[2], CommentsPath: args[3]);
         }


        public static int ExecuteForVsix(string VsixPath, string VsixExtractPath, string OutputPath, string CommentsPath)
        {
            var vsixPath = VsixPath;
            var extractedVsixPath = VsixExtractPath;
            var logPath = OutputPath;
            var lciCommentsDirPath = CommentsPath;

            WarnIfTFSRepoNotPresent(lciCommentsDirPath);

            VsixUtility.CleanExtractedFiles(extractedVsixPath);
            VsixUtility.ExtractVsix(vsixPath, extractedVsixPath);

            // For Testing
            //var vsixPath = @"\\wsr-tc\Drops\NuGet.Signed.AllLanguages\latest-successful\Signed\VSIX\15\NuGet.Tools.vsix";
            //var extractedVsixPath = @"\\nuget\NuGet\Share\ValidationTemp\NuGet.Tools.Vsix\";
            //var logPath = @"\\nuget\NuGet\Share\ValidationTemp";
            //var englishDlls = new string[] { @"\\nuget\NuGet\Share\ValidationTemp\NuGet.Tools.Vsix\NuGet.Options.dll" };

            var englishDlls = GetEnglishDlls(extractedVsixPath);

            Console.WriteLine($"Total English Dlls: {englishDlls.Count()}");

            ExecuteForAllEnglishDlls(lciCommentsDirPath, englishDlls);

            LogErrors(logPath);

            return GetReturnCode();
        }

        public static int ExecuteForArtifacts(string ArtifactsPath, string OutputPath, string CommentsPath)
        {
            var artifactsPath = ArtifactsPath;
            var logPath = OutputPath;
            var lciCommentsDirPath = CommentsPath;

            WarnIfTFSRepoNotPresent(lciCommentsDirPath);

            // For Testing
            //var vsixPath = @"\\wsr-tc\Drops\NuGet.Signed.AllLanguages\latest-successful\Signed\VSIX\15\NuGet.Tools.vsix";
            //var extractedVsixPath = @"\\nuget\NuGet\Share\ValidationTemp\NuGet.Tools.Vsix\";
            //var logPath = @"\\nuget\NuGet\Share\ValidationTemp";
            //var englishDlls = new string[] { @"\\nuget\NuGet\Share\ValidationTemp\NuGet.Tools.Vsix\NuGet.Options.dll" };

            var englishDlls = GetEnglishDlls(artifactsPath, isArtifacts: true);

            Console.WriteLine($"Total English Dlls: {englishDlls.Count()}");

            ExecuteForAllEnglishDlls(lciCommentsDirPath, englishDlls);

            LogErrors(logPath);

            return GetReturnCode();
        }

        private static void ExecuteForAllEnglishDlls(string lciCommentsDirPath, string[] englishDlls)
        {
            ParallelOptions ops = new ParallelOptions { MaxDegreeOfParallelism = _numberOfThreads };
            Parallel.ForEach(englishDlls, ops, englishDll =>
            {
                if (DoesDllContainResourceStrings(englishDll))
                {

                    var translatedDlls = GetTranslatedDlls(Path.GetDirectoryName(englishDll), englishDll)
                        .ToList();

                    Console.WriteLine($"Validating: {englishDll} "+
                        Environment.NewLine +
                        $"\t Contains resource strings: True" +
                        Environment.NewLine +
                        $"\t Translated dlls: {translatedDlls.Count()}" +
                        Environment.NewLine);

                    // Add translated dlls into a collection to filter out localized dlls
                    AddToCollection(_localizedDlls,
                        Path.GetFileName(englishDll));

                    foreach (var translatedDll in translatedDlls)
                    {
                        try
                        {
                            // Add translated dlls into a collection to filter out localized dlls
                            AddToCollection(_localizedDlls,
                                Path.GetFileName(englishDll),
                                Directory.GetParent(translatedDll).Name);

                            CompareAllStrings(englishDll, translatedDll, lciCommentsDirPath);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Validating: {englishDll} " +
                        Environment.NewLine +
                        $"\t Contains resource strings: False" +
                        Environment.NewLine);
                }
            });
        }

        private static void WarnIfTFSRepoNotPresent(string lciCommentsDirPath)
        {
            if (!Directory.Exists(lciCommentsDirPath))
            {
                Console.WriteLine($"WARNING: LCI comments path '{lciCommentsDirPath}' in local TFS repo not found! "+
                    "The reults will not contain any locked strings and the non localized string count will be higher.");
            }
            else
            {
                Console.WriteLine($"INFO: LCI Files found - ");
                foreach (var file in Directory.GetFiles(lciCommentsDirPath))
                {
                    Console.WriteLine($"\t {file}");
                }
            }
        }

        private static string[] GetEnglishDlls(string root, bool isArtifacts = false)
        {
            if (isArtifacts)
            {
                var files = new List<string>();
                var directories = Directory.GetDirectories(root)
                    .Where(d => Path.GetFileName(d).StartsWith("NuGet", StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileName(d).StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase));

                foreach (var dir in directories)
                {
                    var expectedDllName = Path.GetFileName(dir) + ".dll";
                    if (Path.GetFileName(dir).Equals("Microsoft.Web.Xdt.2.1.1"))
                    {
                        expectedDllName = "Microsoft.Web.XmlTransform.dll";
                    }

                    var englishDlls = Directory.GetFiles(dir, expectedDllName, SearchOption.AllDirectories)
                        .Where(p => p.Contains("bin") || (Path.GetFileName(dir).StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) && p.Contains("lib")))
                        .Where(p => !p.Contains("ilmerge"))
                        .OrderBy(p => p);

                    if (englishDlls.Any())
                    {
                        files.Add(englishDlls.First());
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: No English dll matching the directory name was found in {dir}");
                    }
                }

                return files.ToArray();
            }
            else
            {
                return Directory.GetFiles(root, "*.dll", SearchOption.TopDirectoryOnly)
                    .Where(f => Path.GetFileName(f).StartsWith("NuGet", StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileName(f).StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
        }

        private static string[] GetTranslatedDlls(string rootDir, string englishDllPath)
        {
            var englishDllName = Path.GetFileNameWithoutExtension(englishDllPath);
            return Directory.GetFiles(rootDir, $"{englishDllName}.resources.dll", SearchOption.AllDirectories);
        }

        private static bool CompareAllStrings(string firstDll, string secondDll, string lciCommentDirPath)
        {
            var lciFilePath = Path.Combine(lciCommentDirPath, Path.GetFileName(firstDll) + ".lci");
            XElement lciFile = null;
            if (File.Exists(lciFilePath))
            {
                lciFile = XElement.Load(lciFilePath);
            }
            else
            {
                Console.WriteLine($"WARNING: No LCI file found at {lciFilePath}");
            }

            var result = true;

            var firstAssembly = Assembly.LoadFrom(firstDll);
            var firstAssemblyResourceFullNames = GetResourceFullNamesFromDll(firstAssembly);

            var secondAssembly = Assembly.LoadFrom(secondDll);
            var secondAssemblyResourceFullNames = GetResourceFullNamesFromDll(secondAssembly);

            foreach (var firstAssemblyResourceFullName in firstAssemblyResourceFullNames)
            {

                var firstResourceSetEnumerator = GetResourceEnumeratorFromAssembly(firstAssemblyResourceFullName, firstAssembly);

                while (firstResourceSetEnumerator.MoveNext())
                {
                    if (IsResourceAValidString(firstResourceSetEnumerator.Key, firstResourceSetEnumerator.Value))
                    {
                        if (IsResourceStringURIOrNonAlphabetical(firstResourceSetEnumerator.Key as string, firstResourceSetEnumerator))
                        {
                            continue;
                        }

                        var lciEntries = GetLciEntries(lciFile, firstResourceSetEnumerator.Key as string);

                        if (lciEntries?.Any() == true)
                        {
                            var lciCommentAndValueTuple = GetLciCommentAndValueString(lciEntries);
                            var cmtString = lciCommentAndValueTuple.Item1;
                            var valueString = lciCommentAndValueTuple.Item2;

                            if (cmtString.Contains("Locked"))
                            {
                                var lockedString = $"Dll: {firstAssemblyResourceFullName}{Environment.NewLine}" +
                                        $"'{firstResourceSetEnumerator.Key}':'{firstResourceSetEnumerator.Value}' {Environment.NewLine}" +
                                        $"lcx:{cmtString}{Environment.NewLine}" +
                                       "================================================================================================================";
                                _lockedStrings.Enqueue(lockedString);
                            }

                            if (IsStringResourceLocked(cmtString, valueString))
                            {
                                continue;
                            }
                        }

                        var secodResourceFullName = secondAssemblyResourceFullNames
                            .First(r => r.StartsWith(GetResourceNameFromFullName(firstAssemblyResourceFullName)));

                        var secondResource = GetResourceFromAssembly(secodResourceFullName, 
                            firstResourceSetEnumerator.Key as string, 
                            secondAssembly);

                        if (secondResource == null)
                        {
                            var error = $"Resource '{firstResourceSetEnumerator.Key}' from english resource set '{firstAssemblyResourceFullName}' " +
                                $"MISSING in dll '{secondDll}'{Environment.NewLine}" +
                                $"'{firstResourceSetEnumerator.Key}':'{firstResourceSetEnumerator.Value}'{Environment.NewLine}" +
                                "================================================================================================================";
                            _missingLocalizedErrors.Enqueue(error);
                            result = false;
                        }
                        else if (!CompareStrings(firstResourceSetEnumerator.Value as string, secondResource))
                        {
                            var error = $"Resource '{firstResourceSetEnumerator.Key}' from english resource set '{firstAssemblyResourceFullName}' " +
                                $"NOT SAME in dll '{secondDll}'{Environment.NewLine}" +
                                $"'{firstResourceSetEnumerator.Key}':'{firstResourceSetEnumerator.Value}' {Environment.NewLine}" +
                                $"'{firstResourceSetEnumerator.Key}':'{secondResource}'{Environment.NewLine}" +
                                "================================================================================================================";
                            _misMatcherrors.Enqueue(error);
                            result = false;
                        }
                        else if (secondResource.Equals(firstResourceSetEnumerator.Value as string))
                        {
                            var error = $"Resource '{firstResourceSetEnumerator.Key}' from english resource set '{firstAssemblyResourceFullName}' " +
                                    $"EXACTLY SAME in dll '{secondDll}'{Environment.NewLine}" +
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

        private static bool IsResourceStringURIOrNonAlphabetical(string resourceKey, IDictionaryEnumerator resourceSetEnumerator)
        {
            Uri uriResult;
            if ((Uri.TryCreate((resourceSetEnumerator.Value as string), UriKind.Absolute, out uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp)) ||
                (resourceSetEnumerator.Value as string).All(c => !char.IsLetter(c)))
            {
                return true;
            }
            return false;
        }

        private static bool IsResourceAValidString(object resourceKey, object resourceValue)
        {
            if ((resourceKey is string) &&
                !((resourceKey.ToString()).StartsWith(">>")) &&
                (resourceValue is string))
            {
                return true;
            }
            return false;
        }

        private static IEnumerable<XElement> GetLciEntries(XElement lciFile, string resourceStringKey)
        {
            return lciFile
                ?.Descendants()
                .Where(d => d.Name.LocalName.Equals("Item", StringComparison.OrdinalIgnoreCase))
                .Where(d => d.Attribute(XName.Get("ItemId")).Value.Equals(";" + resourceStringKey, StringComparison.OrdinalIgnoreCase));
        }

        private static Tuple<string, string> GetLciCommentAndValueString(IEnumerable<XElement> lciEntries)
        {
            var lciEntry = lciEntries.First();
            var valueData = lciEntry
                .Descendants()
                .Where(d => d.Name.LocalName.Equals("val", StringComparison.OrdinalIgnoreCase));
            var valueString = ((XCData)valueData.First().FirstNode).Value;

            var cmtData = lciEntry.Descendants()
                .Where(d => d.Name.LocalName.Equals("cmt", StringComparison.OrdinalIgnoreCase));
            var cmtString = ((XCData)cmtData.First().FirstNode).Value;

            return new Tuple<string, string>(cmtString, valueString);
        }

        public static bool IsStringResourceLocked(string cmtString, string valueString)
        {
            if (cmtString.Equals("{Locked}", StringComparison.OrdinalIgnoreCase))              
            {
                return true;
            }
            else
            {
                var lockedSubStrings = GetLockedSubStrings(cmtString);
                var valueStringCopy = string.Copy(valueString);
                foreach(var lockedSubString in lockedSubStrings)
                {
                    if (valueStringCopy.Contains(lockedSubString))
                    {
                        valueStringCopy = valueStringCopy.Replace(lockedSubString, string.Empty);
                    }
                }
                if(string.IsNullOrEmpty(valueStringCopy) || valueStringCopy.All(c => !char.IsLetter(c)))
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<string> GetLockedSubStrings(string cmtString)
        {
            var lockedSubStrings = new List<string>();
            var commentStrings = GetStringResourceComments(cmtString);
            foreach (var commentString in commentStrings)
            {
                var commentStringSplit = commentString.Split('=');
                var type = commentStringSplit[0];
                var comments = commentStringSplit[1];
                if (type.Contains("Locked"))
                {
                    var subStrings = comments.Split(',');
                    subStrings.ToList().ForEach(s => lockedSubStrings.Add(CleanedUpLockedStrings(s.Trim())));

                }
            }
            return lockedSubStrings;
        }

        private static string CleanedUpLockedStrings(string lockedString)
        {
            if (lockedString.EndsWith("}"))
            {
                lockedString = lockedString.Substring(0, lockedString.Length - 1);
            }
            if (lockedString.StartsWith("\""))
            {
                lockedString = lockedString.Substring(1, lockedString.Length - 2);
            }
            if (lockedString.EndsWith("\""))
            {
                lockedString = lockedString.Substring(0, lockedString.Length - 2);
            }
            return lockedString;
        }

        private static IEnumerable<string> GetStringResourceComments(string cmtString)
        {
            var commentSubStrings = new List<string>();
            for (int i = 0; i < cmtString.Length; i++)
            {
                var ch = cmtString[i];
                if (ch == '{')
                {
                    var endLocation = cmtString.IndexOf('}', i);
                    commentSubStrings.Add(cmtString.Substring(i, endLocation - i + 1));
                }
            }
            return commentSubStrings;
        }

        private static bool DoesDllContainResourceStrings(string dll)
        {
            var assembly = Assembly.LoadFrom(dll);
            return GetResourceFullNamesFromDll(assembly).Any();
        }

        private static IEnumerable<string> GetResourceFullNamesFromDll(Assembly assembly)
        {
            return assembly
                .GetManifestResourceNames()
                .Where(r => r.EndsWith(".resources", StringComparison.OrdinalIgnoreCase) && 
                           !r.EndsWith("g.resources", StringComparison.OrdinalIgnoreCase));
        }

        private static IDictionaryEnumerator GetResourceEnumeratorFromAssembly(string resourceFullName, Assembly assembly)
        {
            var assemblyResourceName = resourceFullName
                .Substring(0, resourceFullName.LastIndexOf(".resource", StringComparison.OrdinalIgnoreCase));

            var resourceManager = new ResourceManager(assemblyResourceName, assembly);

            var resourceSet = resourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);

            return resourceSet.GetEnumerator();
        }

        private static ResourceSet GetResourceSetFromAssembly(string resourceFullName, Assembly assembly)
        {
            var assemblyResourceName = GetResourceNameFromFullName(resourceFullName);

            var resourceManager = new ResourceManager(assemblyResourceName, assembly);

            return resourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
        }

        private static string GetResourceNameFromFullName(string resourceFullName)
        {
            return resourceFullName
                .Substring(0, resourceFullName.LastIndexOf(".resource", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetResourceFromAssembly(string resourceFullName, string resourceKey, Assembly assembly)
        {
            var resourceSet = GetResourceSetFromAssembly(resourceFullName, assembly);
            return resourceSet.GetString(resourceKey);
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
            lock (_packageStringCollectionLock)
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

        private static void AddToCollection(Dictionary<string, List<string>> collection, 
            string dllName)
        {
            lock (_packageDllCollectionLock)
            {
                if (!collection.ContainsKey(dllName))
                {
                    collection[dllName] = new List<string>();
                }
            }
        }

        private static void AddToCollection(Dictionary<string, List<string>> collection, 
            string dllName, string language)
        {
            lock (_packageDllCollectionLock)
            {
                if (collection.ContainsKey(dllName))
                {
                    if (!collection[dllName].Contains(language))
                    {
                        collection[dllName].Add(language.ToLower());
                    }
                }
                else
                {
                    collection[dllName] = new List<string> { language.ToLower() };
                }
            }
        }


        private static int GetReturnCode()
        {
            int result = 0;

            if (_nonLocalizedStringErrors.Any())
            {
                // Currently these are treated as non fatal errors
                result = result == 1 ? 1 : 0;
            }
            if (_misMatcherrors.Any())
            {
                // Currently these are treated as non fatal errors
                result = result == 1 ? 1 : 0;
            }
            if (_missingLocalizedErrors.Any())
            {
                // These are treated as fatal errors
                result = 1;
            }
            if (_nonLocalizedStringErrorsDeduped.Keys.Any())
            {
                // These are treated as fatal errors
                result = 1;
            }
            if (_localizedDlls.Keys.Where(key => _localizedDlls[key].Count() != _languages.Count()).Any())
            {
                // These are treated as fatal errors
                result = 1;
            }

            return result;
        }

        private static void LogErrors(string logPath)
        {
            if (!Directory.Exists(logPath))
            {
                Console.WriteLine($"INFO: Creating new Director for logs at '{logPath}'");
                Directory.CreateDirectory(logPath);
            }

            LogErrors(logPath, 
                _nonLocalizedStringErrors, 
                "Not_Localized_Strings", 
                "These Strings are same as English strings.");

            LogErrors(logPath, 
                _misMatcherrors, 
                "Mismatch_Strings", 
                "These Strings do not contain the same number of place holders as the English strings.");

            LogErrors(logPath, 
                _missingLocalizedErrors, 
                "Missing_Strings", 
                "These Strings are missing in the localized resources.");

            LogErrors(logPath,
                _lockedStrings,
                "Locked_Strings",
                "These are wholly locked or contain a locked sub string.");

            LogCollectionToXslt(logPath,
                _nonLocalizedStringErrorsDeduped,
                "Not_Localized_Strings_Deduped",
                "These Strings are same as English strings.");

            LogCollectionToXslt(logPath,
                _localizedDlls,
                "Not_Localized_Dlls_Deduped",
                "These Dlls have not been localized.");
        }

        private static void LogErrors(string logPath, 
            ConcurrentQueue<string>errors, 
            string errorType, 
            string errorDescription)
        {
            if (errors.Any())
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
                    foreach (var error in errors)
                    {
                        w.WriteLine(error);
                    }
                }
            }
        }

        private static void LogCollectionToXslt(string logPath, 
            Dictionary<string, Dictionary<string, List<string>>> collection, 
            string logFileName, 
            string logDescription)
        {
            if (collection.Keys.Any())
            {
                var path = Path.Combine(logPath, logFileName + ".csv");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Error Type: {logFileName} - {logDescription}");
                Console.WriteLine($"Unique non-translated count: {collection.Keys.Count}");
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
                            foreach (var language in _languages)
                            {
                                line.Append(collection[dll][resource].Contains(language) ? "Error" : "");
                                line.Append(",");
                            }

                            w.WriteLine(line.ToString());
                        }
                    }
                }
            }
        }

        private static void LogCollectionToXslt(string logPath, 
           Dictionary<string, List<string>> collection, 
            string logFileName,
            string logDescription)
        {
            if (collection.Keys.Where(key => collection[key].Count() != _languages.Count()).Any())
            {
                var path = Path.Combine(logPath, logFileName + ".csv");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Error Type: {logFileName} - {logDescription}");
                Console.WriteLine($"Unique non-translated count: {collection.Keys.Where(key => collection[key].Count() != _languages.Count()).Count()}");
                Console.WriteLine($"Errors logged at: {path}");
                Console.WriteLine("================================================================================================================");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                using (StreamWriter w = File.AppendText(path))
                {
                    w.WriteLine("Dll Name, cs, de, es, fr, it, ja, ko, pl, pt-br, ru, tr, zh-hans, zh-hant");
                    foreach (var dll in collection.Keys)
                    {
                        if (collection[dll].Count != _languages.Count())
                        {
                            var line = new StringBuilder();
                            line.Append(dll);
                            line.Append(",");
                            foreach (var language in _languages)
                            {
                                line.Append(!collection[dll].Contains(language) ? "Error" : "");
                                line.Append(",");
                            }

                            w.WriteLine(line.ToString());
                        }
                    }
                }
            }
        }

        private static void LogCollectionToXsltSimple(string logPath,
            Dictionary<string, Dictionary<string, List<string>>> collection,
            string logFileName,
            string logDescription)
        {
            if (collection.Keys.Any())
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
}
