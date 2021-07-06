using System;
using System.Collections.Generic;
using Needle.XR.ARSimulation;
using Needle.XR.ARSimulation.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Needle.XR.ARSimulation
{
    public class SimulatedARMeshing : MonoBehaviour
    {
        private class Chunk : IMeshProvider
        {
            public Mesh Mesh { get; private set; }
            public ulong Id => (ulong) GetHashCode();

            private readonly Dictionary<Collider, List<int>> tris = new Dictionary<Collider, List<int>>();

            public bool HasCollider(Collider col)
            {
                return tris.ContainsKey(col);
            }

            public bool Contains(Collider col, int triangle)
            {
                return tris.ContainsKey(col) && tris[col].Contains(triangle);
            }

            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> indices = new List<int>();
            private readonly List<Vector3> normals = new List<Vector3>();

            private readonly Dictionary<int, Vector3> offsets = new Dictionary<int, Vector3>();

            public void ClearData()
            {
                tris.Clear();
                vertices.Clear();
                indices.Clear();
                normals.Clear();
                Mesh.Clear();
                offsets.Clear();
            }

            public void Add(Collider col, int triangleIndex, Mesh mesh, Matrix4x4 localToWorld, float noiseStrength = .1f)
            {
                if (!tris.ContainsKey(col)) tris.Add(col, new List<int>());
                tris[col].Add(triangleIndex);

                if (!trianglesCache.ContainsKey(mesh)) trianglesCache.Add(mesh, mesh.triangles);
                if (!verticesCache.ContainsKey(mesh)) verticesCache.Add(mesh, mesh.vertices);
                if (!normalsCache.ContainsKey(mesh)) normalsCache.Add(mesh, mesh.normals);

                var triangles = trianglesCache[mesh];
                var t1 = triangles[triangleIndex * 3];
                var t2 = triangles[triangleIndex * 3 + 1];
                var t3 = triangles[triangleIndex * 3 + 2];

                var _normals = normalsCache[mesh];
                Vector3
                    n1,// = localToWorld.MultiplyVector(_normals[t1]),
                    n2,// = localToWorld.MultiplyVector(_normals[t2]),
                    n3;// = localToWorld.MultiplyVector(_normals[t3]);


                int CustomHash(Vector3 vec)
                {
                    const int precision = 1000;
                    return ((int) vec.x * precision).GetHashCode() ^ ((int) vec.y * precision).GetHashCode() << 2 ^
                           ((int) vec.z * precision).GetHashCode() >> 2;
                }

                void ApplyNoise(ref Vector3 p, Vector3 normal, float strength = .1f)
                {
                    var key = CustomHash(p);
                    if (!offsets.ContainsKey(key))
                    {
                        Vector3 offset = Vector3.zero;
                        if (strength >= 0.01)
                        {
                            offset = Random.insideUnitSphere * strength;
                            var dir = Vector3.Dot(offset, normal);
                            offset *= Mathf.Clamp01(dir);
                        }
                        offsets.Add(key, offset + normal * 0.01f);
                    }
                    p += offsets[key];
                }

                var verts = verticesCache[mesh];

                Vector3 AddVertex(int index)
                {
                    indices.Add(vertices.Count);
                    var vert = localToWorld.MultiplyPoint(verts[index]);
                    var normal = localToWorld.MultiplyVector(_normals[index]);
                    ApplyNoise(ref vert, normal, noiseStrength);
                    vertices.Add(vert);
                    return vert;
                }

                var v1 = AddVertex(t1);
                var v2 = AddVertex(t2);
                var v3 = AddVertex(t3);

                var calculatedNormal = Vector3.Cross(v1 - v2, v2 - v3);
                calculatedNormal.Normalize();
                n1 = n2 = n3 = calculatedNormal;

                normals.Add(n1);
                normals.Add(n2);
                normals.Add(n3);

                if (Mesh == null) Mesh = new Mesh();
                Mesh.SetVertices(vertices);
                Mesh.SetNormals(normals);
                Mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
                Mesh.UploadMeshData(false);
            }

            // public void ReduceNoise(Collider col, int triangle, Mesh mesh)
            // {
            //     // TODO not sure if LiDAR meshes improve over time - if so we could reduce the added noise when sampled multiple times
            // }

            public void NotifyUpdatedOrCreated()
            {
                MeshManager.RegisterOrUpdate(this);
            }

            public void Remove()
            {
                MeshManager.Unregister(this);
            }
        }

        private static readonly List<Chunk> generatedChunks = new List<Chunk>();

        private static readonly Dictionary<Mesh, int[]> trianglesCache = new Dictionary<Mesh, int[]>();
        private static readonly Dictionary<Mesh, Vector3[]> verticesCache = new Dictionary<Mesh, Vector3[]>();

        private static readonly Dictionary<Mesh, Vector3[]> normalsCache = new Dictionary<Mesh, Vector3[]>();
        // private static readonly Dictionary<Mesh, int> indicesCache = new Dictionary<Mesh, int>();

        private static Camera ARCamera => PlacementHelper.ARCamera;
        private static Ray Ray(Vector2 sp) => ARCamera ? ARCamera.ScreenPointToRay(sp) : new Ray();


        public float MaxRange = 5;
        [Range(1, 40)] public int SamplesPerFrame = 1;
        [Range(0, 2)] public float UpdateFrequency = 1;
        [Range(0, 0.5f)] public float NoiseStrength = 0f;

        private readonly HashSet<Chunk> chunksUpdated = new HashSet<Chunk>();
        private float lastUpdate;

        private void Update()
        {
            for (var i = 0; i < SamplesPerFrame; i++)
                Sample(new Vector2(Screen.width * Random.value, Screen.height * Random.value));
            if (Time.time - lastUpdate > UpdateFrequency)
            {
                lastUpdate = Time.time;
                foreach (var ch in chunksUpdated)
                    ch.NotifyUpdatedOrCreated();
                chunksUpdated.Clear();
            }
        }

        private void Sample(Vector2 sp)
        {
            if (!Physics.Raycast(Ray(sp), out var hit)) return;
            if (PlacementHelper.IsTrackable(hit.transform)) return;
            if (hit.distance > MaxRange) return;

            var mc = hit.collider as MeshCollider;
            if (!mc || mc == null) return;
            var triangle = hit.triangleIndex;

            Chunk ch = null;
            foreach (var chunk in generatedChunks)
            {
                if (!chunk.HasCollider(mc)) continue;
                ch = chunk;
                break;
            }

            if (ch != null && ch.Contains(mc, triangle)) return;

            var mesh = mc.sharedMesh;

            if (ch == null)
            {
                ch = new Chunk();
                generatedChunks.Add(ch);
            }

            ch.Add(mc, triangle, mesh, hit.transform.localToWorldMatrix, NoiseStrength);
            if (!chunksUpdated.Contains(ch)) chunksUpdated.Add(ch);
        }

        private void OnDisable()
        {
            ClearDataAndRemoveAll();
        }

        [ContextMenu(nameof(ClearDataAndRemoveAll))]
        private void ClearDataAndRemoveAll()
        {
            foreach (var entry in generatedChunks)
            {
                entry.ClearData();
                entry.Remove();
            }

            generatedChunks.Clear();
            chunksUpdated.Clear();
        }
    }
}