using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.SLO.MaterialsCombine
{
    public enum BackupOption { StoreInComponent, CreateBackupObject }

    [RequireComponent(typeof(Renderer))]
    public class MaterialCombineSourceBackup : MonoBehaviour
    {
        public bool BackupCreated
        {
            get
            {
                return _backupCreated;
            }
        }
        public BackupOption BackupOption
        {
            get
            {
                return _backupOption;
            }
            set
            {
                if (BackupCreated)
                {
                    Debug.Log("Can't change backup option since backup already created. Revert first");
                    return;
                }

                _backupOption = value;
            }
        }

        [SerializeField]
        private Mesh _storedMesh;

        [SerializeField]
        private Material[] _storedMaterials;

        [SerializeField]
        private GameObject _backupObject;

        [SerializeField]
        public BackupOption _backupOption;

        [SerializeField]
        private bool _backupCreated;


        public void CreateBackup()
        {
            if (_backupCreated)
            {
                Debug.Log("MaterialCombineSourceBackup::" + gameObject.name + " backup already created. Revert first");
                return;
            }

            if (!ReadyToCreateBackup(out string failureMessage))
            {
                Debug.Log("MaterialCombineSourceBackup::" + gameObject.name + " can't gather data: " + failureMessage);
                return;
            }

            if (_backupOption == BackupOption.StoreInComponent)
            {
                StoreDataInComponent();

                _backupCreated = true;
            }
            else if (_backupOption == BackupOption.CreateBackupObject)
            {
                CreateBackupObject();

                _backupCreated = true;
            }
            else
                throw new ArgumentException("MaterialCombineSourceBackup::unknown BackupOption");
        }

        public void RevertMaterial(Material revertMaterial)
        {
            if (!CheckBackup(out string failureMessage))
            {
                Debug.Log("MaterialCombineSourceBackup::" + gameObject.name + " can't revert material: " + failureMessage);

                DestroyBackup();

                _backupCreated = false;

                return;
            }

            Renderer renderer = GetComponent<Renderer>();
            Material[] originalMaterials = null;

            Mesh sourceMesh = null;
            Mesh destinationMesh = null;

            if (_backupOption == BackupOption.StoreInComponent)
            {
                sourceMesh = _storedMesh;
                originalMaterials = _storedMaterials;

                if (renderer is SkinnedMeshRenderer)
                {
                    destinationMesh = (renderer as SkinnedMeshRenderer).sharedMesh;
                }
                else if (renderer is MeshRenderer)
                {
                    destinationMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            else if (_backupOption == BackupOption.CreateBackupObject)
            {
                originalMaterials = _backupObject.GetComponent<Renderer>().sharedMaterials;

                if (renderer is SkinnedMeshRenderer)
                {
                    sourceMesh = _backupObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
                    destinationMesh = (renderer as SkinnedMeshRenderer).sharedMesh;
                }
                else if (renderer is MeshRenderer)
                {
                    sourceMesh = _backupObject.GetComponent<MeshFilter>().sharedMesh;
                    destinationMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                }
            }

            Material[] sharedMaterials = renderer.sharedMaterials;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                if (sharedMaterials[i] == revertMaterial)
                {
                    CopyUV(sourceMesh, destinationMesh, i);
                    sharedMaterials[i] = originalMaterials[i];

                    Debug.Log($"Material {originalMaterials[i].name} reverted");
                }
            }

            renderer.sharedMaterials = sharedMaterials;
        }

        public void RevertAll()
        {
            if (!CheckBackup(out string message))
            {
                Debug.Log("MaterialCombineSourceBackup::" + gameObject.name + " can't revert all: " + message);

                DestroyBackup();

                _backupCreated = false;

                return;
            }

            Renderer renderer = GetComponent<Renderer>();

            if (_backupOption == BackupOption.StoreInComponent)
            {
                renderer.sharedMaterials = _storedMaterials;

                if (renderer is SkinnedMeshRenderer)
                {
                    (renderer as SkinnedMeshRenderer).sharedMesh = _storedMesh;
                }
                else if (renderer is MeshRenderer)
                {
                    renderer.GetComponent<MeshFilter>().sharedMesh = _storedMesh;
                }
            }
            else if (_backupOption == BackupOption.CreateBackupObject)
            {
                renderer.sharedMaterials = _backupObject.GetComponent<Renderer>().sharedMaterials;

                if (renderer is SkinnedMeshRenderer)
                {
                    (renderer as SkinnedMeshRenderer).sharedMesh = _backupObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
                }
                else if (renderer is MeshRenderer)
                {
                    renderer.GetComponent<MeshFilter>().sharedMesh = _backupObject.GetComponent<MeshFilter>().sharedMesh;
                }
            }

            Debug.Log($"'{gameObject.name}' mesh and materials reverted");
        }

        public void DestroyBackup()
        {
            if (_backupObject != null)
                DestroyImmediate(_backupObject);

            _storedMesh = null;
            _storedMaterials = null;

            _backupCreated = false;
        }


        private bool ReadyToCreateBackup(out string failureMessage)
        {
            Renderer renderer = GetComponent<Renderer>();

            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();

                if (filter == null)
                {
                    failureMessage = "MeshFilter is missing";
                    return false;
                }

                if (filter.sharedMesh == null)
                {
                    failureMessage = "Mesh is missing";
                    return false;
                }

                failureMessage = "";
                return true;
            }
            else if (renderer is SkinnedMeshRenderer)
            {
                if ((renderer as SkinnedMeshRenderer).sharedMesh == null)
                {
                    failureMessage = "Mesh is missing";
                    return false;
                }

                failureMessage = "";
                return true;
            }
            else
            {
                failureMessage = "Unsupported renderer type";
                return false;
            }
        }

        private bool CheckBackup(out string failureMessage)
        {
            if (!BackupCreated)
            {
                failureMessage = "Backup Not Created";
                return false;
            }

            if (_backupOption == BackupOption.StoreInComponent)
            {
                if (_storedMesh == null)
                {
                    failureMessage = "Stored Mesh Is Missed";
                    return false;
                }

                if (_storedMaterials == null)
                {
                    failureMessage = "Stored Materials Is Missed";
                    return false;
                }
            }
            else if (_backupOption == BackupOption.CreateBackupObject)
            {
                if (_backupObject == null)
                {
                    failureMessage = "Backup Object Is Missed";
                    return false;
                }
            }

            failureMessage = "";
            return true;
        }

        private void StoreDataInComponent()
        {
            Renderer renderer = GetComponent<Renderer>();

            _storedMaterials = renderer.sharedMaterials;

            if (renderer is MeshRenderer)
            {
                _storedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer)
            {
                _storedMesh = (renderer as SkinnedMeshRenderer).sharedMesh;
            }
        }

        private void CreateBackupObject()
        {
            _backupObject = new GameObject(gameObject.name + "_Backup");

            _backupObject.transform.SetParent(transform, false);
            _backupObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
            _backupObject.transform.localScale = Vector3.one;

            Renderer sourceRenderer = GetComponent<Renderer>();

            if (sourceRenderer is MeshRenderer)
            {
                MeshRenderer backupRenderer = _backupObject.AddComponent<MeshRenderer>();
                MeshFilter backupFilter = _backupObject.AddComponent<MeshFilter>();

                MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                backupFilter.sharedMesh = sourceFilter.sharedMesh;

                backupRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

                CopyCommonRendererProperties(sourceRenderer, backupRenderer);
            }
            else if (sourceRenderer is SkinnedMeshRenderer)
            {
                SkinnedMeshRenderer sourceSkinned = sourceRenderer as SkinnedMeshRenderer;
                SkinnedMeshRenderer backupSkinned = _backupObject.AddComponent<SkinnedMeshRenderer>();

                backupSkinned.sharedMesh = sourceSkinned.sharedMesh;
                backupSkinned.sharedMaterials = sourceSkinned.sharedMaterials;
                backupSkinned.bones = sourceSkinned.bones;
                backupSkinned.rootBone = sourceSkinned.rootBone;

                CopyCommonRendererProperties(sourceSkinned, backupSkinned);

                backupSkinned.updateWhenOffscreen = sourceSkinned.updateWhenOffscreen;
            }
            else
            {
                Debug.LogError("CreateBackupObject: Unsupported renderer type: " + sourceRenderer.GetType());
                return;
            }

            _backupObject.SetActive(false);
        }

        private void CopyCommonRendererProperties(Renderer source, Renderer destination)
        {
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;

            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;

            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;

            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingLayerName = source.sortingLayerName;
            destination.sortingOrder = source.sortingOrder;

            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;

            destination.lightmapIndex = source.lightmapIndex;
            destination.realtimeLightmapIndex = source.realtimeLightmapIndex;
            destination.lightmapScaleOffset = source.lightmapScaleOffset;
            destination.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
        }

        private void CopyUV(Mesh source, Mesh destination, int subMeshIndex)
        {
            int[] indices = source.GetIndices(subMeshIndex);

            Vector2[] srcUV = source.uv;
            Vector2[] destUV = destination.uv;

            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];

                destUV[idx] = srcUV[idx];
            }

            destination.uv = destUV;
        }
    }
}
