using System;
using Reflex.Core;
using UnityEngine;

public class RootInstaller : MonoBehaviour, IInstaller
{
    [SerializeField] InputManager inputManagerPrefab;

    public void InstallBindings(ContainerBuilder builder)
    {
        // InputManager'ı sahneler arası kalıcı bir singleton gibi bind ediyoruz
        var instance = Instantiate(inputManagerPrefab);
        DontDestroyOnLoad(instance);

        builder.RegisterValue(instance, new Type[]
        {
            typeof(IInputProvider),
            typeof(InputManager)
        });
    }
}
