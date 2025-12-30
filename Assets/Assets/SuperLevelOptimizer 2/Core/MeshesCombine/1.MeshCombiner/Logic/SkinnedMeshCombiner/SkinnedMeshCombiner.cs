using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace NGS.SLO.MeshesCombine
{
    public class SkinnedMeshCombiner : ISkinnedMeshCombiner
    {
        private const int MAX_UV_CHANNELS = 8;

        public MeshCombineResult Combine(IList<MeshCombineInstance> instances, out Transform[] bones)
        {
            try
            {
                if (instances == null || instances.Count == 0)
                {
                    bones = null;
                    return new MeshCombineResult(instances.ToArray(), "SkinnedMeshCombiner::instances is empty");
                }

                foreach (var instance in instances)
                {
                    if (!instance.ReadyForCombine)
                    {
                        bones = null;
                        return new MeshCombineResult(instances.ToArray(), "Not all instances ready for combine");
                    }
                }


                int[] verticesRemap = null;
                int vertexCount = 0;

                int[] indices = CombineIndices(instances, out verticesRemap, out vertexCount);       
                Vector3[] vertices = CombineVertices(instances, verticesRemap, vertexCount);

                Mesh combined = new Mesh();
                combined.name = CreateCombinedMeshName(instances);

                if (vertices.Length >= UInt16.MaxValue)
                    combined.indexFormat = IndexFormat.UInt32;

                combined.subMeshCount = 1;

                combined.SetVertices(vertices);
                combined.SetIndices(indices, MeshTopology.Triangles, 0);

                if (AnyHasNormals(instances))
                    combined.SetNormals(CombineNormals(instances, verticesRemap, vertexCount));

                if (AnyHasTangents(instances))
                    combined.SetTangents(CombineTangents(instances, verticesRemap, vertexCount));

                for (int i = 0; i < MAX_UV_CHANNELS; i++)
                {
                    if (AnyHasUV(instances, i))
                        combined.SetUVs(i, CombineUVs(instances, i, verticesRemap, vertexCount));
                }

                Matrix4x4[] bindposes = null;
                Dictionary<int, int> oldToNewBones = null;

                bones = CombineBones(instances, out bindposes, out oldToNewBones);

                combined.boneWeights = CombineBoneWeights(instances, oldToNewBones, verticesRemap, vertexCount);
                combined.bindposes = bindposes;

                return new MeshCombineResult(instances.ToArray(), combined);
            }
            catch (Exception ex)
            {
                bones = null;
                return new MeshCombineResult(instances.ToArray(), $"{ex.Message} \n {ex.StackTrace}");
            }
        }

        private int[] CombineIndices(IList<MeshCombineInstance> instances, out int[] verticesRemap, out int usedVertexCount)
        {
            int totalIndices = instances.Sum(i => i.indicesCount);
            int totalVertices = instances.Sum(i => i.vertexCount);

            int[] indices = new int[totalIndices];
            verticesRemap = Enumerable.Repeat(-1, totalVertices).ToArray();

            List<int> tempIndices = new List<int>();

            int indexOffset = 0;
            int vertexOffset = 0;

            int vertexIndex = 0;

            foreach (var instance in instances)
            {
                instance.mesh.GetIndices(tempIndices, instance.submeshIndex);

                for (int i = 0; i < tempIndices.Count; i++)
                {
                    int oldIndex = tempIndices[i] + vertexOffset;
                    int newIndex = verticesRemap[oldIndex];

                    if (newIndex == -1)
                    {
                        newIndex = vertexIndex;

                        verticesRemap[oldIndex] = newIndex;

                        vertexIndex++;
                    }

                    indices[i + indexOffset] = newIndex;
                }

                vertexOffset += instance.vertexCount;
                indexOffset += instance.indicesCount;
            }

            usedVertexCount = vertexIndex;
            return indices;
        }

        private Vector3[] CombineVertices(IList<MeshCombineInstance> instances, int[] verticesRemap, int vertexCount)
        {
            Vector3[] vertices = new Vector3[vertexCount];
            List<Vector3> tempVertices = new List<Vector3>();

            int vertexOffset = 0;

            foreach (var instance in instances)
            {
                Mesh mesh = instance.mesh;

                mesh.GetVertices(tempVertices);

                RemapVertexAttribute(tempVertices, vertices, verticesRemap, vertexOffset);

                vertexOffset += mesh.vertexCount;
            }

            return vertices;
        }

        private Vector3[] CombineNormals(IList<MeshCombineInstance> instances, int[] verticesRemap, int vertexCount)
        {
            Vector3[] normals = new Vector3[vertexCount];
            List<Vector3> tempNormals = new List<Vector3>();

            int vertexOffset = 0;

            foreach (var instance in instances)
            {
                Mesh mesh = instance.mesh;

                mesh.GetNormals(tempNormals);

                if (tempNormals.Count == 0)
                    FillListWithValues(tempNormals, new Vector3(0, 1, 0), mesh.vertexCount);

                RemapVertexAttribute(tempNormals, normals, verticesRemap, vertexOffset);

                vertexOffset += mesh.vertexCount;
            }

            return normals;
        }

        private Vector4[] CombineTangents(IList<MeshCombineInstance> instances, int[] verticesRemap, int vertexCount)
        {
            Vector4[] tangents = new Vector4[vertexCount];
            List<Vector4> tempTangents = new List<Vector4>();

            int vertexOffset = 0;

            foreach (var instance in instances)
            {
                Mesh mesh = instance.mesh;

                mesh.GetTangents(tempTangents);

                if (tempTangents.Count == 0)
                    FillListWithValues(tempTangents, new Vector4(1, 0, 0, 1), mesh.vertexCount);

                RemapVertexAttribute(tempTangents, tangents, verticesRemap, vertexOffset);

                vertexOffset += mesh.vertexCount;
            }

            return tangents;
        }

        private Vector2[] CombineUVs(IList<MeshCombineInstance> instances, int channel, int[] verticesRemap, int vertexCount)
        {
            Vector2[] uvs = new Vector2[vertexCount];
            List<Vector2> tempUvs = new List<Vector2>();

            int vertexOffset = 0;

            foreach (var instance in instances)
            {
                Mesh mesh = instance.mesh;

                mesh.GetUVs(channel, tempUvs);

                if (tempUvs.Count == 0)
                    FillListWithValues(tempUvs, new Vector2(0, 0), mesh.vertexCount);

                RemapVertexAttribute(tempUvs, uvs, verticesRemap, vertexOffset);

                vertexOffset += mesh.vertexCount;
            }

            return uvs;
        }

        private Transform[] CombineBones(IList<MeshCombineInstance> instances, out Matrix4x4[] bindposes, out Dictionary<int, int> oldToNewBones)
        {
            List<Transform> bonesList = new List<Transform>();
            List<Matrix4x4> bindposesList = new List<Matrix4x4>();
            
            oldToNewBones = new Dictionary<int, int>();

            int boneOffset = 0;

            for (int i = 0; i < instances.Count; i++)
            {
                Transform[] instanceBones = (instances[i].renderer as SkinnedMeshRenderer).bones;
                Matrix4x4[] instanceBindposes = instances[i].mesh.bindposes;

                for (int c = 0; c < instanceBones.Length; c++)
                {
                    Transform bone = instanceBones[c];
                    Matrix4x4 bindpose = instanceBindposes[c];

                    int oldBoneIndex = boneOffset + c;
                    int newBoneIndex = -1;

                    for (int j = 0; j < bonesList.Count; j++)
                    {
                        if (bonesList[j] == bone)
                        {
                            if (bindposesList[j].Equals(bindpose))
                            {
                                newBoneIndex = j;
                                break;
                            }
                        }
                    }

                    if (newBoneIndex < 0)
                    {
                        bonesList.Add(bone);
                        bindposesList.Add(instanceBindposes[c]);

                        newBoneIndex = bonesList.Count - 1;
                    }

                    oldToNewBones.Add(oldBoneIndex, newBoneIndex);
                }

                boneOffset += instanceBones.Length;
            }

            bindposes = bindposesList.ToArray();
            return bonesList.ToArray();
        }

        private BoneWeight[] CombineBoneWeights(IList<MeshCombineInstance> instances, Dictionary<int, int> oldToNewBones, int[] verticesRemap, int vertexCount)
        {
            BoneWeight[] weights = new BoneWeight[vertexCount];
            List<BoneWeight> tempWeights = new List<BoneWeight>();

            int vertexOffset = 0;
            int boneOffset = 0;

            foreach (var instance in instances)
            {
                Mesh mesh = instance.mesh;
                mesh.GetBoneWeights(tempWeights);

                if (tempWeights.Count == 0)
                    FillListWithValues(tempWeights, default, mesh.vertexCount);

                for (int i = 0; i < tempWeights.Count; i++)
                {
                    BoneWeight weight = tempWeights[i];

                    weight.boneIndex0 = oldToNewBones[weight.boneIndex0 + boneOffset];
                    weight.boneIndex1 = oldToNewBones[weight.boneIndex1 + boneOffset]; 
                    weight.boneIndex2 = oldToNewBones[weight.boneIndex2 + boneOffset]; 
                    weight.boneIndex3 = oldToNewBones[weight.boneIndex3 + boneOffset]; 

                    tempWeights[i] = weight;
                }

                RemapVertexAttribute(tempWeights, weights, verticesRemap, vertexOffset);

                vertexOffset += instance.vertexCount;
                boneOffset += (instance.renderer as SkinnedMeshRenderer).bones.Length;
            }

            return weights;
        }


        private string CreateCombinedMeshName(IList<MeshCombineInstance> instances)
        {
            string name = "";

            int count = Mathf.Min(3, instances.Count);

            for (int i = 0; i < count; i++)
            {
                string rendererName = instances[i].renderer.name;

                if (rendererName.Length > 5)
                    rendererName = rendererName.Remove(5);

                name += $"{rendererName}_sub_{instances[i].submeshIndex}_";
            }

            name = name.Remove(name.Length - 1);

            return name;
        }

        private bool AnyHasNormals(IList<MeshCombineInstance> instances)
        {
            List<Vector3> normals = new List<Vector3>();

            foreach (var instance in instances)
            {
                instance.mesh.GetNormals(normals);

                if (normals.Count > 0)
                    return true;
            }

            return false;
        }

        private bool AnyHasTangents(IList<MeshCombineInstance> instances)
        {
            List<Vector4> tangents = new List<Vector4>();

            foreach (var instance in instances)
            {
                instance.mesh.GetTangents(tangents);

                if (tangents.Count > 0)
                    return true;
            }

            return false;
        }

        private bool AnyHasUV(IList<MeshCombineInstance> instances, int channel)
        {
            List<Vector2> uvs = new List<Vector2>();

            foreach (var instance in instances)
            {
                instance.mesh.GetUVs(channel, uvs);

                if (uvs.Count > 0)
                    return true;
            }

            return false;
        }

        private void FillListWithValues<T>(List<T> list, T value, int count)
        {
            for (int i = 0; i < count; i++)
                list.Add(value);
        }

        private void RemapVertexAttribute<T>(IList<T> source, IList<T> destination, int[] verticesRemap, int vertexOffset)
        {
            for (int i = 0; i < source.Count; i++)
            {
                int index = verticesRemap[i + vertexOffset];

                if (index == -1)
                    continue;

                destination[index] = source[i];
            }
        }
    }
}

//private void CombineSkinnedComponents(Mesh combinedMesh, IList<MeshCombineInstance> instances, out List<Transform> combinedBones)
//{
//    SkinnedMeshRenderer[] renderers = instances.Select(i => i.renderer as SkinnedMeshRenderer).ToArray();

//    // Collect Bones
//    combinedBones = new List<Transform>();
//    Dictionary<Transform, int> boneToIndex = new Dictionary<Transform, int>();

//    for (int i = 0; i < renderers.Length; i++)
//    {
//        foreach (var bone in renderers[i].bones)
//        {
//            if (bone != null && !boneToIndex.ContainsKey(bone))
//            {
//                boneToIndex.Add(bone, combinedBones.Count);
//                combinedBones.Add(bone);
//            }
//        }
//    }

//    // Collect Bone Weights
//    List<BoneWeight> combinedBoneWeights = new List<BoneWeight>();
//    foreach (var renderer in renderers)
//    {
//        Mesh mesh = renderer.sharedMesh;
//        BoneWeight[] meshBoneWeights = mesh.boneWeights;
//        int[] localToCombined = new int[renderer.bones.Length];

//        for (int i = 0; i < renderer.bones.Length; i++)
//            localToCombined[i] = boneToIndex[renderer.bones[i]];

//        for (int i = 0; i < mesh.vertexCount; i++)
//        {
//            BoneWeight bw = meshBoneWeights[i];
//            BoneWeight newBw = new BoneWeight
//            {
//                boneIndex0 = localToCombined[bw.boneIndex0],
//                weight0 = bw.weight0,

//                boneIndex1 = localToCombined[bw.boneIndex1],
//                weight1 = bw.weight1,

//                boneIndex2 = localToCombined[bw.boneIndex2],
//                weight2 = bw.weight2,

//                boneIndex3 = localToCombined[bw.boneIndex3],
//                weight3 = bw.weight3
//            };

//            combinedBoneWeights.Add(newBw);
//        }
//    }



//    // Collect bind poses
//    Matrix4x4[] combinedBindPoses = new Matrix4x4[combinedBones.Count];
//    Dictionary<Transform, Matrix4x4> boneToBindPose = new Dictionary<Transform, Matrix4x4>();

//    foreach (var smr in renderers)
//    {
//        for (int i = 0; i < smr.bones.Length; i++)
//        {
//            Transform bone = smr.bones[i];
//            boneToBindPose[bone] = smr.sharedMesh.bindposes[i];
//        }
//    }

//    for (int i = 0; i < combinedBones.Count; i++)
//        combinedBindPoses[i] = boneToBindPose[combinedBones[i]];

//    if (combinedBoneWeights.Count != combinedMesh.vertexCount)
//        combinedBoneWeights.RemoveRange(combinedMesh.vertexCount, combinedBoneWeights.Count - combinedMesh.vertexCount - 1);

//    combinedMesh.boneWeights = combinedBoneWeights.ToArray();
//    combinedMesh.bindposes = combinedBindPoses;
//}
