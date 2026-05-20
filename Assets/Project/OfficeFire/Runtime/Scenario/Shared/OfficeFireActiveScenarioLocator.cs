using WOI.Modules.SDK;

namespace Woi.OfficeFire
{
    /// <summary>
    /// Resolves the currently active <see cref="OfficeFireScenarioController"/> via <see cref="ServiceLocator"/>.
    /// Registered by <see cref="OfficeFireScenarioBootstrapper"/> when a scenario starts.
    /// </summary>
    public static class OfficeFireActiveScenarioLocator
    {
        public static bool TryGetActive(out OfficeFireScenarioController controller)
        {
            controller = null;
            if (!ServiceLocator.TryGet(out OfficeFireScenarioController registered) || registered == null)
            {
                return false;
            }

            if (!registered.IsScenarioActive)
            {
                return false;
            }

            controller = registered;
            return true;
        }

        public static void RegisterActive(OfficeFireScenarioController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (ServiceLocator.IsRegistered<OfficeFireScenarioController>())
            {
                ServiceLocator.Unregister<OfficeFireScenarioController>();
            }

            ServiceLocator.Register(controller);
        }

        public static void UnregisterIfActive(OfficeFireScenarioController controller)
        {
            if (controller == null)
            {
                return;
            }

            if (ServiceLocator.TryGet(out OfficeFireScenarioController current) &&
                ReferenceEquals(current, controller))
            {
                ServiceLocator.Unregister<OfficeFireScenarioController>();
            }
        }
    }
}
