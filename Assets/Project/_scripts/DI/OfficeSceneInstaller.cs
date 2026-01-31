using Reflex.Core;
using Reflex.Enums;
using Resolution = Reflex.Enums.Resolution;
using UnityEngine;
using Woi.Player;
using Woi.HazardSystem;

namespace Woi.Utils.DI
{
    public class OfficeSceneInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private HazardManager hazardManager;

        public void InstallBindings(ContainerBuilder builder)
        {
            builder.RegisterFactory<IPlayerService>(
                _ => new PlayerService(),
                Lifetime.Singleton,
                Resolution.Lazy
            );

            builder.RegisterValue(hazardManager,
            new[] { typeof(HazardManager),
                   typeof(IHazardManagerService) });
        }
    }
}
