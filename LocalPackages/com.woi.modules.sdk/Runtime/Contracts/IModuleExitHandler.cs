namespace WOI.Modules.SDK.Contracts
{
    /// <summary>
    /// Uniformly defines exactly how a running simulation politely requests safely gracefully 
    /// tearing down its scene footprint back out into the core Hub UI shell.
    /// </summary>
    public interface IModuleExitHandler
    {
        /// <summary>
        /// Explicit intent trigger invoked dynamically by the module (e.g. from a gameplay UI generic Back button)
        /// cleanly shifting control flow backwards.
        /// </summary>
        void RequestModuleExit();
    }
}
