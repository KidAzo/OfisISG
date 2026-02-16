using System;
using Reflex.Core;
using UnityEngine;
using Woi.Porting;
using Woi.Settings;

public class RootInstaller : MonoBehaviour, IInstaller
{
    [SerializeField] InputManager inputManagerPrefab;
    [SerializeField] SceneLoader sceneLoaderPrefab;
    [SerializeField] GameManager gameManagerPrefab;
    [SerializeField] PortingController portingControllerPrefab;

    public void InstallBindings(ContainerBuilder builder)
    {
        var inputManagerInstance = Instantiate(inputManagerPrefab);
        var sceneLoaderInstance = Instantiate(sceneLoaderPrefab);
        var gameManagerInstance = Instantiate(gameManagerPrefab);
        var portingControllerInstance = Instantiate(portingControllerPrefab);
        
        DontDestroyOnLoad(gameManagerInstance);
        DontDestroyOnLoad(inputManagerInstance);
        DontDestroyOnLoad(sceneLoaderInstance);
        DontDestroyOnLoad(portingControllerInstance);
        
        builder.RegisterValue(portingControllerInstance, new Type[]
        {
            typeof(IPortingService),
            typeof(PortingController)
        });

        builder.RegisterValue(inputManagerInstance, new Type[]
        {
            typeof(IInputProvider),
            typeof(InputManager)
        });

        builder.RegisterValue(sceneLoaderInstance, new Type[]
        {
            typeof(ISceneLoaderService),
            typeof(SceneLoader)
        });

        builder.RegisterValue(gameManagerInstance, new Type[]
        {
            typeof(IGameManager),
            typeof(GameManager)
        });
    }
}
