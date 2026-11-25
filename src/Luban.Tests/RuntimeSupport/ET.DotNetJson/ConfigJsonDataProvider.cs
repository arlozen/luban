// Copyright 2025 Code Philosophy

using System.Text.Json;

namespace ET
{
    public interface IConfigJsonDataProvider
    {
        JsonElement GetJsonElement(string name);
        JsonElement GetJsonElement(string packageId, string dataKey);
    }

    public sealed class FileConfigJsonDataProvider : IConfigJsonDataProvider
    {
        private readonly string _dataDir;

        public FileConfigJsonDataProvider(string dataDir)
        {
            _dataDir = dataDir;
        }

        public JsonElement GetJsonElement(string packageId, string dataKey)
        {
            return GetJsonElement(System.IO.Path.Combine(packageId, dataKey));
        }

        public JsonElement GetJsonElement(string name)
        {
            var path = System.IO.Path.Combine(_dataDir, name + ".json");
            using var document = JsonDocument.Parse(System.IO.File.ReadAllText(path));
            return document.RootElement.Clone();
        }
    }
}
