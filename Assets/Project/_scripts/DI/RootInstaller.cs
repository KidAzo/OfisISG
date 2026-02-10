using System;
using Reflex.Core;
using UnityEditor.SearchService;
using UnityEngine;
using Woi.Settings;

public class RootInstaller : MonoBehaviour, IInstaller
{
    [SerializeField] InputManager inputManagerPrefab;
    [SerializeField] SceneLoader sceneLoaderPrefab;

    public void InstallBindings(ContainerBuilder builder)
    {
        // InputManager'ı sahneler arası kalıcı bir singleton gibi bind ediyoruz
        var inputManagerInstance = Instantiate(inputManagerPrefab);
        var sceneLoaderInstance = Instantiate(sceneLoaderPrefab);
        DontDestroyOnLoad(inputManagerInstance);
        DontDestroyOnLoad(sceneLoaderInstance);

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
    }
}
