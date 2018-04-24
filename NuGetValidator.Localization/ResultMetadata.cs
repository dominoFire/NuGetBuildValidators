
using Newtonsoft.Json.Linq;

namespace NuGetValidator.Localization
{
    internal class ResultMetadata
    {
        public string Type { get; set; }

        public string Description { get; set; }

        public int ErrorCount { get; set; }

        public string Path { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["Type"] = Type,
                ["Description"] = Description,
                ["ErrorCount"] = ErrorCount,
                ["Path"] = Path
            };
        }
    }
}
