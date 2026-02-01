using Reflex.Core;
using Reflex.Enums;
using Resolution = Reflex.Enums.Resolution;
using UnityEngine;
using Woi.Player;
using Woi.HazardSystem;
using WoiUtils.AudioSystem;
using Woi.PopUpSystem;

namespace Woi.Utils.DI
{
    public class OfficeSceneInstaller : MonoBehaviour, IInstaller
    {
        [SerializeField] private HazardManager hazardManager;
        [SerializeField] private AudioSystem audioSystem;
        [SerializeField] private PopupManager popupManager;

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

            builder.RegisterValue(audioSystem,
                  new[] { typeof(AudioSystem),
                   typeof(AudioSystem) });

            builder.RegisterValue(popupManager,
           new[] { typeof(PopupManager),
                   typeof(PopupManager) });
        }
    }
}