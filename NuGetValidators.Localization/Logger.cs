using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGetValidators.Localization
{
    internal static class Logger
    {
        internal static void LogErrors(
            string logPath, 
            ConcurrentQueue<string> nonLocalizedStringErrors, 
            ConcurrentQueue<string> mismatchErrors, 
            ConcurrentQueue<string> missingLocalizedErrors, 
            ConcurrentQueue<string> lockedStrings, 
            Dictionary<string, Dictionary<string, List<string>>> nonLocalizedStringErrorsDeduped, 
            Dictionary<string, List<string>> localizedDlls,
            HashSet<string> languages)
        {
            if (!Directory.Exists(logPath))
            {
                Console.WriteLine($"INFO: Creating new directory for logs at '{logPath}'");
                Directory.CreateDirectory(logPath);
            }

            LogErrors(logPath,
                nonLocalizedStringErrors,
                "NotLocalizedStrings",
                "These Strings are same as English strings.");

            LogErrors(logPath,
                mismatchErrors,
                "MismatchStrings",
                "These Strings do not contain the same number of placeholders as the English strings.");

            LogErrors(logPath,
                missingLocalizedErrors,
                "MissingStrings",
                "These Strings are missing in the localized dlls.");

            LogErrors(logPath,
                lockedStrings,
                "LockedStrings",
                "These are wholly locked or contain a locked sub string.");

            LogCollectionToXslt(logPath,
                nonLocalizedStringErrorsDeduped,
                languages,
                "NotLocalizedStringsUnique",
                "These Strings are same as English strings.");

            LogCollectionToXslt(logPath,
                localizedDlls,
                languages,
                "NotLocalizedDllUnique",
                "These Dlls have not been localized.");
        }

        private static void LogErrors(string logPath,
            ConcurrentQueue<string> errors,
            string errorType,
            string errorDescription)
        {
            if (errors.Any())
            {
                var path = Path.Combine(logPath, errorType + ".txt");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Type: {errorType} - {errorDescription}");
                Console.WriteLine($"Total count: {errors.Count()}");
                Console.WriteLine($"Logged at: {path}");
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
            HashSet<string> languages,
            string logFileName,
            string logDescription)
        {
            if (collection.Keys.Any())
            {
                var path = Path.Combine(logPath, logFileName + ".csv");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Type: {logFileName} - {logDescription}");
                Console.WriteLine($"Unique non-translated count: {collection.Keys.Count}");
                Console.WriteLine($"Logged at: {path}");
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
                            foreach (var language in languages)
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
            HashSet<string> languages,
            string logFileName,
            string logDescription)
        {
            if (collection.Keys.Where(key => collection[key].Count() != languages.Count()).Any())
            {
                var path = Path.Combine(logPath, logFileName + ".csv");

                Console.WriteLine("================================================================================================================");
                Console.WriteLine($"Type: {logFileName} - {logDescription}");
                Console.WriteLine($"Unique non-translated count: {collection.Keys.Where(key => collection[key].Count() != languages.Count()).Count()}");
                Console.WriteLine($"Logged at: {path}");
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
                        if (collection[dll].Count != languages.Count())
                        {
                            var line = new StringBuilder();
                            line.Append(dll);
                            line.Append(",");
                            foreach (var language in languages)
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
                Console.WriteLine($"Type: {logFileName} - {logDescription}");
                Console.WriteLine($"Logged at: {path}");
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