using UnityEngine;
using WoiUtils.AudioSystem;
using WOI.Modules.SDK;

public class FeedbackManager : MonoBehaviour
{
    [SerializeField] private SoundDefinition collectSound;

    private AudioSystem audioSystem;

    private void Awake()
    {
        if (!ServiceLocator.IsRegistered<FeedbackManager>())
            ServiceLocator.Register(this);
    }

    private void Start()
    {
        if (AudioSystem.TryGetFromServiceLocator(out audioSystem) && audioSystem != null)
            return;

        audioSystem = FindFirstObjectByType<AudioSystem>();
        if (audioSystem == null)
            Debug.LogWarning("[FeedbackManager] AudioSystem not found on ServiceLocator or in loaded scenes.", this);
    }

    private void OnDestroy()
    {
        if (ServiceLocator.TryGet(out FeedbackManager registered) && ReferenceEquals(registered, this))
            ServiceLocator.Unregister<FeedbackManager>();
    }

    public void PlayFeedback(Transform source)
    {
        PlaySound(source);
    }

    public void PlaySound(Transform source = null)
    {
        if (collectSound == null)
        {
            Debug.LogWarning("[FeedbackManager] Collect sound is not assigned.", this);
            return;
        }

        if (audioSystem == null)
            return;

        if (source != null)
            audioSystem.Play(collectSound, PlayContext.At(source.position));
        else
            audioSystem.Play(collectSound);
    }
}
