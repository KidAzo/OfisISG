using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Utils
{
    public static class ColliderUtils
    {
        public static Collider Duplicate(Collider original)
        {
            if (original == null)
                throw new ArgumentNullException("ColliderUtils::Duplicate 'original' collider is null");

            GameObject newObj = new GameObject("SC_Collider");
            Collider newCollider = (Collider)newObj.AddComponent(original.GetType());

            CopyColliderProperties(original, newCollider);

            return newCollider;
        }

        public static void CopyColliderProperties(Collider original, Collider copy)
        {
            copy.isTrigger = original.isTrigger;
            copy.sharedMaterial = original.sharedMaterial;

            if (original is BoxCollider)
            {
                CopyBoxColliderProperties((BoxCollider)original, (BoxCollider)copy);
            }
            else if (original is SphereCollider)
            {
                CopySphereColliderProperties((SphereCollider)original, (SphereCollider)copy);
            }
            else if (original is CapsuleCollider)
            {
                CopyCapsuleColliderProperties((CapsuleCollider)original, (CapsuleCollider)copy);
            }
            else if (original is MeshCollider)
            {
                CopyMeshColliderProperties((MeshCollider)original, (MeshCollider)copy);
            }
            else if (original is TerrainCollider)
            {
                CopyTerrainColliderProperties((TerrainCollider)original, (TerrainCollider)copy);
            }
            else
            {
                Debug.Log(string.Format("ColliderUtils::CopyColliderProperties {0} type not implemented", original.GetType()));
            }
        }


        private static void CopyBoxColliderProperties(BoxCollider original, BoxCollider copy)
        {
            copy.center = original.center;
            copy.size = original.size;
        }

        private static void CopySphereColliderProperties(SphereCollider original, SphereCollider copy)
        {
            copy.center = original.center;
            copy.radius = original.radius;
        }

        private static void CopyCapsuleColliderProperties(CapsuleCollider original, CapsuleCollider copy)
        {
            copy.center = original.center;
            copy.height = original.height;
            copy.radius = original.radius;
            copy.direction = original.direction;
        }

        private static void CopyMeshColliderProperties(MeshCollider original, MeshCollider copy)
        {
            copy.sharedMesh = original.sharedMesh;
            copy.convex = original.convex;
            copy.cookingOptions = original.cookingOptions;
        }

        private static void CopyTerrainColliderProperties(TerrainCollider original, TerrainCollider copy)
        {
            copy.terrainData = original.terrainData;
        }
    }
}
