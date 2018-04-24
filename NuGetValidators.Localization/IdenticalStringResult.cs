﻿
using Newtonsoft.Json.Linq;

namespace NuGetValidators.Localization
{
    internal class IdenticalStringResult : StringCompareResult
    {
        public string EnglishValue { get; set; }

        public string LocalizedValue { get; set; }

        public override JObject ToJson()
        {
            var json = base.ToJson();
            json["EnglishValue"] = EnglishValue;
            json["LocalizedValue"] = LocalizedValue;

            return json;
        }
    }
}