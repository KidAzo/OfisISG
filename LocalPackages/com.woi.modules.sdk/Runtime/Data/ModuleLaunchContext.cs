using System.Collections.Generic;

namespace WOI.Modules.SDK.Data
{
    /// <summary>
    /// Payload passed from the Hub into a module after the module entry scene is loaded.
    /// </summary>
    public class ModuleLaunchContext
    {
        public string ModuleId { get; set; }

        public ModuleDefinition TargetModule { get; set; }

        /// <summary>Optional key/value metadata from the Hub (not serialized by default).</summary>
        public IReadOnlyDictionary<string, object> Metadata { get; set; }
    }
}
