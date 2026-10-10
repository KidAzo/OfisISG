using UnityEngine;
using Woi.HazardSystem;

namespace HazardSystem.NPC
{
    public class NpcResetter : MonoBehaviour
    {
        [SerializeField] Transform[] npcs;
        [SerializeField] string resetStateName = "Walking";
        [SerializeField] float resetDelay = 2f;
        Vector3[] positions;
        Quaternion[] rotations;
        Hazard hazard;
        NPCAnimationController[] animationController;

        void Start()
        {
            hazard = GetComponent<Hazard>();
            
            SetStartLocomotion();    
        }
  
        public void ResetNpcs()
        {
            for (int i = 0; i < npcs.Length; i++)
            {
                if (i < positions.Length)
                {
                    npcs[i].position = positions[i];
                    npcs[i].rotation = rotations[i];
                    animationController[i].ChangeState(resetStateName);
                }
            }
        }

        public void ResetNpcsWithDelay()
        {
            if (hazard.IsFixed) return;
            
            Invoke(nameof(ResetNpcs), resetDelay);
        }

       void SetStartLocomotion()
        {
            positions = new Vector3[npcs.Length];
            rotations = new Quaternion[npcs.Length];
            animationController = new NPCAnimationController[npcs.Length];

            for (int i = 0; i < npcs.Length; i++)
            {
                positions[i] = npcs[i].position;
                rotations[i] = npcs[i].rotation;
                animationController[i] = npcs[i].GetComponent<NPCAnimationController>();
            }
        }
    }
}

