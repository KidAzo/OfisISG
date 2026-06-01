namespace Woi.SelectionSystem
{
    /// <summary>
    /// Optional gate — when <see cref="CanSelect"/> is false, <see cref="SelectionSystemManager"/> ignores input.
    /// </summary>
    public interface ISelectionInputGate
    {
        bool CanSelect { get; }
    }
}
