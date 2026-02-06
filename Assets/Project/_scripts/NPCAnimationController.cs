using UnityEngine;

public class NPCAnimationController : MonoBehaviour
{
    Animator animator;

    [Header("Animator State")]
    [SerializeField] private string stateName;   // Inspector’dan girilecek
    [SerializeField] private int layer = 0;
    [SerializeField] private float fadeTime = 0.1f;
    [SerializeField] bool playOnStart = true;
    private int stateHash;

    void Start()
    {
        animator = GetComponent<Animator>();

        if (playOnStart)
        {
            stateHash = Animator.StringToHash(stateName);
            SetAnimationState(stateHash);
        }
    }

    void SetAnimationState(int hash)
    {
        animator.CrossFade(hash, fadeTime, layer);
    }

    public void ChangeState(string newStateName)
    {
        Debug.Log($"Changing animation state to: {newStateName}");  
        int newStateHash = Animator.StringToHash(newStateName);
        SetAnimationState(newStateHash);
    }
}
