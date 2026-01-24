using Reflex.Core;
using Reflex.Enums;
using Resolution = Reflex.Enums.Resolution;
using UnityEngine;
using Woi.Player;

namespace Woi.Utils.DI
{
    public class OfficeSceneInstaller : MonoBehaviour, IInstaller
    {
        public void InstallBindings(ContainerBuilder builder)
        {
                builder.RegisterFactory<IPlayerService>(
                    _ => new PlayerService(),
                    Lifetime.Singleton,
                    Resolution.Lazy
                );
        }
    }
}
