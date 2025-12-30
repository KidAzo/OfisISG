using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Static
{
    public class CameraZone : MonoBehaviour
    {
        public static List<CameraZone> Instances { get; private set; }

        [field: SerializeReference]
        public VisibilityTree VisibilityTree { get; private set; }

        [field: SerializeField]
        public int CellsCount { get; private set; }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReloadDomain()
        {
            if (Instances != null)
                Instances.Clear();
        }

        private void Awake()
        {
            if (Instances == null)
                Instances = new List<CameraZone>();

            Instances.Add(this);
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
        }


        public bool CreateVisibilityTree(float cellSize)
        {
            if (cellSize < 0.01f)
            {
                Debug.Log("Unable to create VisibilityTree with so small cell size : " + cellSize);
                return false;
            }

            if (VisibilityTree != null)
                ClearVisibilityTree();

            Vector3 position = transform.position;
            Vector3 size = transform.lossyScale;

            size.x = Mathf.Abs(size.x);
            size.y = Mathf.Abs(size.y);
            size.z = Mathf.Abs(size.z);

            int countX = Mathf.CeilToInt(size.x / cellSize);
            int countY = Mathf.CeilToInt(size.y / cellSize);
            int countZ = Mathf.CeilToInt(size.z / cellSize);

            if (countX == 0 || countY == 0 || countZ == 0)
            {
                Debug.Log("Unable to create VisibilityTree with side size equals zero");
                return false;
            }

            try
            {
                VisibilityTree = new VisibilityTree(cellSize);

                Vector3 start = (position - size / 2);

                for (int x = 0; x < countX; x++)
                {
                    for (int y = 0; y < countY; y++)
                    {
                        for (int z = 0; z < countZ; z++)
                        {
                            Vector3 offset = new Vector3()
                            {
                                x = cellSize * x,
                                y = cellSize * y,
                                z = cellSize * z
                            };
    
                            VisibilityTree.Add(start + offset);
                        }
                    }
                }

                CellsCount = countX * countY * countZ;
            }
            catch(Exception ex)
            {
                Debug.Log("Unable to create VisibilityTree, reason : " + ex.Message + ex.StackTrace);
                return false;
            }

            return true;
        }

        public void ClearVisibilityTree()
        {
            VisibilityTree = null;
            CellsCount = 0;
        }
    }
}
