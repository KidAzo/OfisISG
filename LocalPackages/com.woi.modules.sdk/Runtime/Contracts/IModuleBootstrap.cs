using System.Threading.Tasks;
using WOI.Modules.SDK.Data;

namespace WOI.Modules.SDK.Contracts
{
    /// <summary>
    /// Implemented by a module entry (e.g. bootstrap scene) so the Hub can start gameplay after DI and scenes are ready.
    /// </summary>
    public interface IModuleBootstrap
    {
        /// <summary>
        /// Called by the Hub after the module catalog is loaded, optional RootScope is alive, and the entry scene load has completed.
        /// </summary>
        Task Initialize(ModuleLaunchContext context);
    }
}
