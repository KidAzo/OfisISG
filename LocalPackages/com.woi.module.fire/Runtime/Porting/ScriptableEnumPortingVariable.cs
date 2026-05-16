using UnityEngine;
using Obvious.Soap;

public enum AppMode
{
    PC,
    XR,
}

[CreateAssetMenu(fileName = "ScriptableEnumPortingVariable", menuName = "Soap/ScriptableEnums/PortingVariable")]
public class ScriptableEnumPortingVariable : ScriptableEnumBase
{
    [SerializeField] private AppMode _currentValue;
    public AppMode CurrentValue => _currentValue;

    public void SetCurrentValue(AppMode value)
    {
        _currentValue = value;
    }
}