using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    public enum OccluderType { Collider, Mesh, LODGroup, Terrain }

    public class DC_Occluder : MonoBehaviour
    {
        [field: SerializeField]
        public OccluderType OccluderType { get; set; }

        private Bounds? _bounds;
        private int _layer;


        public bool TryGetBounds(ref Bounds bounds)
        {
            if (_bounds != null)
            {
                bounds = _bounds.Value;
                return true;
            }

            if (OccluderType == OccluderType.Collider)
            {
                if (TryGetComponent(out Collider collider))
                {
                    _bounds = bounds = collider.bounds;
                    return true;
                }
            }
            else if (OccluderType == OccluderType.Mesh)
            {
                if (TryGetComponent(out MeshRenderer renderer))
                {
                    _bounds = bounds = renderer.bounds;
                    return true;
                }
            }
            else
            {
                if (TryGetComponent(out LODGroup group))
                {
                    LOD[] lods = group.GetLODs();

                    for (int i = 0; i < lods.Length; i++)
                    {
                        foreach (var renderer in lods[i].renderers)
                        {
                            if (renderer != null)
                            {
                                _bounds = bounds = renderer.bounds;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }


        private void Reset()
        {
            if (GetComponent<MeshRenderer>() != null)
                OccluderType = OccluderType.Mesh;

            else if (GetComponent<LODGroup>() != null)
                OccluderType = OccluderType.LODGroup;

            else if (GetComponent<Terrain>() != null)
                OccluderType = OccluderType.Terrain;
        }

        private void Start()
        {
            _layer = DC_Controller.GetCullingLayer();

            if (OccluderType == OccluderType.Collider)
            {
                gameObject.layer = _layer;
            }
            else if (OccluderType == OccluderType.Mesh)
            {
                CreateMeshOccluder();
            }
            else if (OccluderType == OccluderType.LODGroup)
            {
                CreateLODGroupOccluder();
            }
            else if (OccluderType == OccluderType.Terrain)
            {
                CreateTerrainOccluder();
            }
        }


        private void CreateMeshOccluder()
        {
            MeshFilter filter = GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                Debug.Log(gameObject.name + " unable to create occluder, mesh not found");
                return;
            }

            CreateCollider(gameObject, filter.sharedMesh);
        }

        private void CreateLODGroupOccluder()
        {
            LODGroup group = GetComponent<LODGroup>();

            if (group == null)
            {
                Debug.Log(gameObject.name + " unable to create occluder, LODGroup not found");
                return;
            }

            LOD lod = group.GetLODs()[0];

            foreach (var renderer in lod.renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                if (filter != null && filter.sharedMesh != null)
                    CreateCollider(renderer.gameObject, filter.sharedMesh);
            }
        }

        private void CreateTerrainOccluder()
        {
            TerrainCollider srcCollider = GetComponent<TerrainCollider>();

            if (srcCollider == null)
            {
                Debug.Log("Unable to create occluder, TerrainCollider not found");
                return;
            }

            GameObject colliderGO = new GameObject("DC_Occluder");

            colliderGO.layer = _layer;
            colliderGO.transform.parent = transform;
            colliderGO.transform.localPosition = Vector3.zero;
            colliderGO.transform.localRotation = Quaternion.identity;
            colliderGO.transform.localScale = Vector3.one;

            TerrainCollider destCollider = colliderGO.AddComponent<TerrainCollider>();
            destCollider.terrainData = srcCollider.terrainData;
        }

        private void CreateCollider(GameObject go, Mesh mesh)
        {
            GameObject colliderGO = new GameObject("DC_Collider");
            Transform colliderTransform = colliderGO.transform;

            colliderGO.layer = _layer;
            colliderTransform.parent = go.transform;
            colliderTransform.localPosition = Vector3.zero;
            colliderTransform.localEulerAngles = Vector3.zero;
            colliderTransform.localScale = Vector3.one;
            
            MeshCollider collider = colliderGO.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
    }
}
