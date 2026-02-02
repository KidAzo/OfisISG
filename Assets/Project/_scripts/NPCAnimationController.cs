using UnityEngine;

public class NPCAnimationController : MonoBehaviour
{
    Animator animator;

    [Header("Animator State")]
    [SerializeField] private string stateName;   // Inspector’dan girilecek
    [SerializeField] private int layer = 0;
    [SerializeField] private float fadeTime = 0.1f;

    private int stateHash;

    void Start()
    {
        animator = GetComponent<Animator>();
        stateHash = Animator.StringToHash(stateName);
        SetAnimationState(stateHash);
    }

    void SetAnimationState(int hash)
    {
        animator.CrossFade(hash, fadeTime, layer);     
    }
}
