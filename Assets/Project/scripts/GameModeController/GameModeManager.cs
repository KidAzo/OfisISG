using System;
using UnityEngine;
using Woi.OfficeFire;
using WOI.Modules.SDK;

namespace Woi.GameMode
{
    public class GameModeManager : MonoBehaviour
    {
        [SerializeField] private GameModeData[] gameModeData;

        private void Start()
        {
            SelectGameMode(ResolveGameMode());
        }

        private OfficeGameModule ResolveGameMode()
        {
            if (!ServiceLocator.TryGet(out OfficeGameModulesBootstrapper bootstrapper) || bootstrapper == null)
            {
                Debug.LogWarning(
                    "[GameModeManager] OfficeGameModulesBootstrapper not found in ServiceLocator — defaulting to FireTraining.",
                    this);
                return OfficeGameModule.FireTraining;
            }

            return bootstrapper.CurrentGameModule;
        }

        private void SelectGameMode(OfficeGameModule gameMode)
        {
            if (gameModeData == null)
            {
                return;
            }

            for (int i = 0; i < gameModeData.Length; i++)
            {
                GameModeData data = gameModeData[i];
                if (data?.gameModeObject == null)
                {
                    continue;
                }

                data.gameModeObject.SetActive(data.gameMode == gameMode);
            }
        }
    }

    [Serializable]
    public class GameModeData
    {
        public OfficeGameModule gameMode;
        public GameObject gameModeObject;
    }
}
