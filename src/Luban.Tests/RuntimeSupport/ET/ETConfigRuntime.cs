// Copyright 2025 Code Philosophy

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

    public abstract class Singleton<T> where T : Singleton<T>
    {
        public static T Instance { get; private set; }

        protected Singleton()
        {
            Instance = (T)this;
        }
    }
}
