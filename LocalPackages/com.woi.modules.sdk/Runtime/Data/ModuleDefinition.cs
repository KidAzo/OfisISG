using System;

namespace WOI.Modules.SDK.Data
{
    /// <summary>
    /// Canonical module metadata for the Hub and module projects. Public fields support Unity JsonUtility DTOs.
    /// </summary>
    [Serializable]
    public class ModuleDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string Version;
        public string EntrySceneKey;
        public string CatalogUrl;
        public string ThumbnailUrl;
        public string RootScopeKey;
    }
}
