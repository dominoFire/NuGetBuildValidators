using Newtonsoft.Json.Linq;

namespace NuGetValidators.Localization
{
    internal class LockedStringResult : StringCompareResult
    {
        public string EnglishValue { get; set; }

        public string LockComment { get; set; }

        public override JObject ToJson()
        {
            var json = base.ToJson();
            json["EnglishValue"] = EnglishValue;
            json["LockComment"] = LockComment;

            return json;
        }
    }
}
