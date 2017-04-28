using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetValidators
{
    public class VsixUtility
    {
        public static void ExtractVsix(string vsixPath, string extractedVsixPath)
        {
            CleanExtractedFiles(extractedVsixPath);

            Console.WriteLine($"Extracting {vsixPath} to {extractedVsixPath}");

            ZipFile.ExtractToDirectory(vsixPath, extractedVsixPath);

            Console.WriteLine($"Done Extracting...");
        }

        public static void CleanExtractedFiles(string path)
        {
            Console.WriteLine("Cleaning up the extracted files");
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
