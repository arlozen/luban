// Copyright 2025 Code Philosophy

using Luban.SimpleJSON;

namespace ET
{
    public interface IConfig
    {
    }

    public interface IConfigCategory
    {
        System.Type ConfigType { get; }
        void ResolveRef();
    }

    public static class ConfigReferenceResolver
    {
        public static void ResolveAll(System.Collections.Generic.IEnumerable<IConfigCategory> categories)
        {
            foreach (var category in categories)
            {
                category.ResolveRef();
            }
        }
    }

    public interface IConfigJsonDataProvider
    {
        JSONNode GetJsonNode(string name);
        JSONNode GetJsonNode(string packageId, string dataKey);
    }

    public abstract class Singleton<T> where T : Singleton<T>
    {
        public static T Instance { get; private set; }

        protected Singleton()
        {
            Instance = (T)this;
        }
    }

    public sealed class FileConfigJsonDataProvider : IConfigJsonDataProvider
    {
        private readonly string _dataDir;

        public FileConfigJsonDataProvider(string dataDir)
        {
            _dataDir = dataDir;
        }

        public JSONNode GetJsonNode(string packageId, string dataKey)
        {
            return GetJsonNode(System.IO.Path.Combine(packageId, dataKey));
        }

        public JSONNode GetJsonNode(string name)
        {
            var path = System.IO.Path.Combine(_dataDir, name + ".json");
            return JSON.Parse(System.IO.File.ReadAllText(path));
        }
    }
}
