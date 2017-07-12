using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetValidators.Utility
{
    public class FileUtility
    {
        public static string[] GetDlls(string root, bool isArtifacts = false)
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
                        Console.WriteLine($"WARNING: No dll matching the directory name was found in {dir}");
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
    }
}
