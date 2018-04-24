using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGetValidators.Localization
{
    internal static class LoggingUtility
    {
        public static void LogErrors(
            string logPath, 
            IEnumerable<StringCompareResult> identicalLocalizedStrings,
            IEnumerable<StringCompareResult> mismatchErrors,
            IEnumerable<StringCompareResult> missingLocalizedErrors,
            IEnumerable<StringCompareResult> lockedStrings, 
            Dictionary<string, Dictionary<string, List<string>>> nonLocalizedStringDeduped, 
            Dictionary<string, LocalizedAssemblyResult> localizedDlls)
        {
            if (!Directory.Exists(logPath))
            {
                Console.WriteLine($"INFO: Creating new directory for logs at '{logPath}'");
                Directory.CreateDirectory(logPath);
            }

            LogErrors(
                logPath,
                identicalLocalizedStrings,
                "NonLocalizedStrings",
                "These Strings are same as English strings.");

            LogErrors(
                logPath,
                mismatchErrors,
                "MismatchStrings",
                "These Strings do not contain the same number of placeholders as the English strings.");

            LogErrors(
                logPath,
                missingLocalizedErrors,
                "MissingStrings",
                "These Strings are missing in the localized dlls.");

            LogErrors(
                logPath,
                lockedStrings,
                "LockedStrings",
                "These are wholly locked or contain a locked sub string.");

            LogNonLocalizedStringsDedupedErrors(
                logPath,
                nonLocalizedStringDeduped,
                "NonLocalizedStringsPerLanguage",
                "These Strings are same as English strings.");

            LogNonLocalizedAssemblyErrors(
                logPath,
                localizedDlls,
                "NonLocalizedAssemblies",
                "These assemblies have not been localized in one or more languages.");

            LogWrongLocalizedAssemblyPathErrors(
                logPath,
                localizedDlls,
                "WrongLocalizedAssemblyPaths",
                "These assemblies do not have localized dlls at the expected locations.");
        }

        private static void LogErrors(
            string logPath,
            IEnumerable<StringCompareResult> errors,
            string errorType,
            string errorDescription)
        {
            if (errors.Any())
            {
                var path = Path.Combine(logPath, errorType + ".json");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Type: {errorType} - {errorDescription}");
                Console.WriteLine($"Count: {errors.Count()}");
                Console.WriteLine($"Path: {path}");
                Console.WriteLine("================================================================================================================");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                using (StreamWriter file = File.AppendText(path))
                {
                    var array = new JArray();
                    foreach (var error in errors)
                    {
                        array.Add(error.ToJson());
                    }

                    var json = new JObject
                    {
                        ["Type"] = errorType,
                        ["Description"] = errorDescription,
                        ["errors"] = array
                    };

                    var settings = new JsonSerializerSettings()
                    {
                        Formatting = Formatting.Indented
                    };

                    var serializer = JsonSerializer.Create(settings);
                    serializer.Serialize(file, json);
                }
            }
        }
        private static void LogWrongLocalizedAssemblyPathErrors(
            string logPath,
            Dictionary<string, LocalizedAssemblyResult> collection,
            string errorType,
            string errorDescription)
        {
            // log errors for when the assembly is not localized in expected languages and at expected paths
            var errors = collection.Keys.Where(key => !collection[key].HasExpectedLocalizedAssemblies());
            if (errors.Any())
            {
                var path = Path.Combine(logPath, errorType + ".json");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Type: {errorType} - {errorDescription}");
                Console.WriteLine($"Count: {errors.Count()}");
                Console.WriteLine($"Path: {path}");
                Console.WriteLine("================================================================================================================");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                using (StreamWriter file = File.AppendText(path))
                {
                    var array = new JArray();
                    foreach (var error in errors)
                    {
                        array.Add(collection[error].ToJson());
                    }

                    var json = new JObject
                    {
                        ["Type"] = errorType,
                        ["Description"] = errorDescription,
                        ["errors"] = array
                    };

                    var settings = new JsonSerializerSettings()
                    {
                        Formatting = Formatting.Indented
                    };

                    var serializer = JsonSerializer.Create(settings);
                    serializer.Serialize(file, json);
                }
            }
        }


        private static void LogNonLocalizedStringsDedupedErrors(
            string logPath,
            Dictionary<string, Dictionary<string, List<string>>> collection,
            string logFileName,
            string logDescription)
        {
            if (collection.Keys.Any())
            {
                var path = Path.Combine(logPath, logFileName + ".csv");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Type: {logFileName} - {logDescription}");
                Console.WriteLine($"Count: {collection.Keys.Count}");
                Console.WriteLine($"Path: {path}");
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
                            foreach (var language in LocaleUtility.LocaleStrings)
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

        private static void LogNonLocalizedAssemblyErrors(
            string logPath,
            Dictionary<string, LocalizedAssemblyResult> collection,
            string logFileName,
            string logDescription)
        {
            // log errors for when the assembly is not localized in enough languages
            var errors = collection.Keys.Where(key => !collection[key].HasAllLocales());
            if (errors.Any())
            {
                var path = Path.Combine(logPath, logFileName + ".csv");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Type: {logFileName} - {logDescription}");
                Console.WriteLine($"Count: {errors.Count()}");
                Console.WriteLine($"Path: {path}");
                Console.WriteLine("================================================================================================================");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                using (StreamWriter w = File.AppendText(path))
                {
                    w.WriteLine("Dll Name, cs, de, es, fr, it, ja, ko, pl, pt-br, ru, tr, zh-hans, zh-hant");
                    foreach (var error in errors)
                    {
                        var assemblyLocales = collection[error].Locales;
                        var line = new StringBuilder();
                        line.Append(error);
                        line.Append(",");
                        foreach (var language in LocaleUtility.LocaleStrings)
                        {
                            line.Append(!assemblyLocales.Contains(language) ? "Error" : "");
                            line.Append(",");
                        }

                        w.WriteLine(line.ToString());
                    }
                }
            }
        }
    }
}