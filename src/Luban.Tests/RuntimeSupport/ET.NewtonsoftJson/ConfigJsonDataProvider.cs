// Copyright 2025 Code Philosophy

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ET
{
    public interface IConfigJsonDataProvider
    {
        JArray GetJsonArray(string name);
        JArray GetJsonArray(string packageId, string dataKey);
    }

    public sealed class FileConfigJsonDataProvider : IConfigJsonDataProvider
    {
        private readonly string _dataDir;

        public FileConfigJsonDataProvider(string dataDir)
        {
            _dataDir = dataDir;
        }

        public JArray GetJsonArray(string packageId, string dataKey)
        {
            return GetJsonArray(System.IO.Path.Combine(packageId, dataKey));
        }

        public JArray GetJsonArray(string name)
        {
            var path = System.IO.Path.Combine(_dataDir, name + ".json");
            using var reader = new JsonTextReader(System.IO.File.OpenText(path)) { DateParseHandling = DateParseHandling.None };
            return JArray.Load(reader);
        }
    }
}
