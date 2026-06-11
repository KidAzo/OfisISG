namespace Woi.InputSystem
{
    /// <summary>
    /// Scene objects that subscribe to <see cref="GameplayInputContext"/> Soap events via a serialized context asset.
    /// <see cref="InputManager.SyncPcPlayerSoapEvents"/> rebinds them to the live context loaded via Addressables.
    /// </summary>
    public interface ISoapGameplayInputContextListener
    {
        bool IsUsingDifferentGameplayInputContext(GameplayInputContext liveContext);

        void RebindGameplayInputContext(GameplayInputContext liveContext);
    }
}
