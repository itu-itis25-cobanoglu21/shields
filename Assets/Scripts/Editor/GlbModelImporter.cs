using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Shields.EditorTools
{
    /// <summary>
    /// Minimal importer for the project's binary glTF (.glb) models: node hierarchy,
    /// one skinned or static mesh per node (POSITION, NORMAL, TEXCOORD_0, JOINTS_0,
    /// WEIGHTS_0, indices) and skins. Materials and animations are not read; every
    /// mesh gets <see cref="material"/>.
    ///
    /// glTF is right-handed; Unity is left-handed. Like other glTF importers this
    /// mirrors X (positions, normals, translations, rotations, bind matrices) and
    /// reverses the triangle order. After that, if most triangles face against their
    /// own stored normals (an exported mirror can leave the faces inside-out), the
    /// winding is reversed again so the faces agree with the normals. Skinned hand
    /// meshes also get their finger skin weights repaired (see FingerWeights). The
    /// source file is never modified.
    /// </summary>
    [ScriptedImporter(4, "glb")]
    public sealed class GlbModelImporter : ScriptedImporter
    {
        [Tooltip("Material given to every imported mesh (the files carry none).")]
        public Material material;

        [Tooltip("Reverse the winding when most triangles face against their stored normals.")]
        public bool fixInsideOutFaces = true;

        [Tooltip("Rebuild smooth normals from the faces sharing each position (the files are flat shaded). Faces meeting at more than this angle keep a crease. 0 keeps the stored normals.")]
        [Range(0f, 180f)]
        public float smoothingAngle = 80f;

        [Tooltip("Hand meshes only. Share of weight from ANOTHER finger's bones that is handed to the finger a vertex actually lies on. Only on the finger itself, fading out toward where the fingers meet the palm; the palm is never changed. 0 = off.")]
        [Range(0f, 1f)]
        public float fingerCrossWeightCleanup = 1f;

        [Tooltip("Hand meshes only. Smooths how each finger vertex's weight is split along its own finger's bones (knuckle transitions). The share that follows the palm or a neighbouring finger is kept. 0 = off.")]
        [Range(0f, 1f)]
        public float fingerWeightSmoothing = 0.25f;

        [Tooltip("How many passes the finger weight smoothing runs.")]
        [Range(1, 10)]
        public int fingerWeightPasses = 1;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] file = File.ReadAllBytes(ctx.assetPath);
            if (file.Length < 20 || BitConverter.ToUInt32(file, 0) != 0x46546C67u)
            {
                ctx.LogImportError("Not a binary glTF file: " + ctx.assetPath);
                return;
            }

            int jsonLength = (int)BitConverter.ToUInt32(file, 12);
            var gltf = (Dictionary<string, object>)MiniJson.Parse(Encoding.UTF8.GetString(file, 20, jsonLength));
            int binStart = 20 + jsonLength + 8;
            var doc = new Gltf(gltf, file, binStart);

            string rootName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var root = new GameObject(rootName);

            List<object> nodes = doc.List("nodes");
            var created = new Transform[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = (Dictionary<string, object>)nodes[i];
                created[i] = new GameObject(n.TryGetValue("name", out object nm) ? (string)nm : "Node" + i).transform;
            }

            var hasParent = new bool[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = (Dictionary<string, object>)nodes[i];
                if (!n.TryGetValue("children", out object ch)) continue;
                foreach (object c in (List<object>)ch)
                {
                    int ci = Convert.ToInt32(c);
                    created[ci].SetParent(created[i], false);
                    hasParent[ci] = true;
                }
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = (Dictionary<string, object>)nodes[i];
                if (!hasParent[i]) created[i].SetParent(root.transform, false);
                float[] t = doc.Floats(n, "translation");
                float[] r = doc.Floats(n, "rotation");
                float[] s = doc.Floats(n, "scale");
                if (t != null) created[i].localPosition = new Vector3(-t[0], t[1], t[2]);
                if (r != null) created[i].localRotation = new Quaternion(r[0], -r[1], -r[2], r[3]);
                if (s != null) created[i].localScale = new Vector3(s[0], s[1], s[2]);
            }

            Material mat = material != null ? material : DefaultMaterial(ctx);
            var meshCache = new Dictionary<int, Mesh>();

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = (Dictionary<string, object>)nodes[i];
                if (!n.TryGetValue("mesh", out object meshIndexObj)) continue;
                int meshIndex = Convert.ToInt32(meshIndexObj);

                if (!meshCache.TryGetValue(meshIndex, out Mesh mesh))
                {
                    bool flipped;
                    mesh = BuildMesh(doc, meshIndex, smoothingAngle, fixInsideOutFaces, out flipped);
                    meshCache[meshIndex] = mesh;
                    ctx.AddObjectToAsset("mesh" + meshIndex, mesh);
                    if (flipped)
                        Debug.Log("[GlbModelImporter] " + ctx.assetPath + ": mesh '" + mesh.name +
                                  "' had faces wound against its normals (inside-out); winding reversed on import.");
                }

                GameObject go = created[i].gameObject;
                if (n.TryGetValue("skin", out object skinIndexObj))
                {
                    var skin = (Dictionary<string, object>)doc.List("skins")[Convert.ToInt32(skinIndexObj)];
                    var joints = (List<object>)skin["joints"];
                    var bones = new Transform[joints.Count];
                    for (int j = 0; j < joints.Count; j++) bones[j] = created[Convert.ToInt32(joints[j])];

                    float[] ibm = doc.Accessor(Convert.ToInt32(skin["inverseBindMatrices"]), out _);
                    var bindposes = new Matrix4x4[joints.Count];
                    for (int j = 0; j < joints.Count; j++) bindposes[j] = MirrorX(ColumnMajor(ibm, j * 16));
                    mesh.bindposes = bindposes;
                    if (fingerCrossWeightCleanup > 0f || fingerWeightSmoothing > 0f)
                        FingerWeights.Fix(mesh, bones, fingerCrossWeightCleanup, fingerWeightSmoothing, fingerWeightPasses);

                    var smr = go.AddComponent<SkinnedMeshRenderer>();
                    smr.sharedMesh = mesh;
                    smr.bones = bones;
                    smr.rootBone = SkeletonRoot(bones);
                    smr.sharedMaterial = mat;
                    smr.localBounds = BoundsInBone(mesh, bindposes[Array.IndexOf(bones, smr.rootBone)]);
                }
                else
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }

            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
        }

        static Material DefaultMaterial(AssetImportContext ctx)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = "GlbDefault" };
            ctx.AddObjectToAsset("defaultMaterial", m);
            return m;
        }

        static Mesh BuildMesh(Gltf doc, int meshIndex, float smoothingAngle, bool fixInsideOut, out bool flippedToNormals)
        {
            var gm = (Dictionary<string, object>)doc.List("meshes")[meshIndex];
            var prims = (List<object>)gm["primitives"];
            if (prims.Count != 1) throw new NotSupportedException("GlbModelImporter supports one primitive per mesh.");
            var prim = (Dictionary<string, object>)prims[0];
            var attrs = (Dictionary<string, object>)prim["attributes"];

            float[] p = doc.Accessor(Convert.ToInt32(attrs["POSITION"]), out _);
            int count = p.Length / 3;
            var positions = new Vector3[count];
            for (int v = 0; v < count; v++) positions[v] = new Vector3(-p[v * 3], p[v * 3 + 1], p[v * 3 + 2]);

            var mesh = new Mesh { name = gm.TryGetValue("name", out object nm) ? (string)nm : "Mesh" + meshIndex };
            if (count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = positions;

            Vector3[] normals = null;
            if (attrs.TryGetValue("NORMAL", out object nIdx))
            {
                float[] nr = doc.Accessor(Convert.ToInt32(nIdx), out _);
                normals = new Vector3[count];
                for (int v = 0; v < count; v++) normals[v] = new Vector3(-nr[v * 3], nr[v * 3 + 1], nr[v * 3 + 2]);
                mesh.normals = normals;
            }

            if (attrs.TryGetValue("TEXCOORD_0", out object uvIdx))
            {
                float[] uv = doc.Accessor(Convert.ToInt32(uvIdx), out _);
                var uvs = new Vector2[count];
                for (int v = 0; v < count; v++) uvs[v] = new Vector2(uv[v * 2], 1f - uv[v * 2 + 1]);
                mesh.uv = uvs;
            }

            if (attrs.TryGetValue("JOINTS_0", out object jIdx) && attrs.TryGetValue("WEIGHTS_0", out object wIdx))
            {
                float[] js = doc.Accessor(Convert.ToInt32(jIdx), out _);
                float[] ws = doc.Accessor(Convert.ToInt32(wIdx), out _);
                var weights = new BoneWeight[count];
                for (int v = 0; v < count; v++)
                {
                    float sum = ws[v * 4] + ws[v * 4 + 1] + ws[v * 4 + 2] + ws[v * 4 + 3];
                    float k = sum > 0f ? 1f / sum : 0f;
                    weights[v] = new BoneWeight
                    {
                        boneIndex0 = (int)js[v * 4], weight0 = ws[v * 4] * k,
                        boneIndex1 = (int)js[v * 4 + 1], weight1 = ws[v * 4 + 1] * k,
                        boneIndex2 = (int)js[v * 4 + 2], weight2 = ws[v * 4 + 2] * k,
                        boneIndex3 = (int)js[v * 4 + 3], weight3 = ws[v * 4 + 3] * k,
                    };
                }
                mesh.boneWeights = weights;
            }

            int[] tris;
            if (prim.TryGetValue("indices", out object iIdx))
            {
                float[] ix = doc.Accessor(Convert.ToInt32(iIdx), out _);
                tris = new int[ix.Length];
                for (int t = 0; t < ix.Length; t++) tris[t] = (int)ix[t];
            }
            else
            {
                tris = new int[count];
                for (int t = 0; t < count; t++) tris[t] = t;
            }

            // Mirroring X turns glTF's counter-clockwise front faces into Unity's back faces.
            ReverseWinding(tris);

            flippedToNormals = false;
            if (fixInsideOut && normals != null)
            {
                int against = 0;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 a = positions[tris[t]], b = positions[tris[t + 1]], c = positions[tris[t + 2]];
                    Vector3 face = Vector3.Cross(b - a, c - a);
                    Vector3 stored = normals[tris[t]] + normals[tris[t + 1]] + normals[tris[t + 2]];
                    if (Vector3.Dot(face, stored) < 0f) against++;
                }
                if (against * 2 > tris.Length / 3)
                {
                    ReverseWinding(tris);
                    flippedToNormals = true;
                }
            }

            mesh.triangles = tris;
            if (normals == null) mesh.RecalculateNormals();
            else if (smoothingAngle > 0f) mesh.normals = SmoothNormals(positions, tris, smoothingAngle);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>
        /// Per-vertex normals averaged over every face that shares the vertex position (weighted
        /// by the face's corner angle there), skipping faces that meet the vertex's own face at
        /// more than <paramref name="creaseAngle"/>. Vertex count and topology are unchanged.
        /// </summary>
        static Vector3[] SmoothNormals(Vector3[] positions, int[] tris, float creaseAngle)
        {
            int faces = tris.Length / 3;
            var faceNormal = new Vector3[faces];
            var cornerWeight = new float[tris.Length];
            var byPosition = new Dictionary<Vector3, List<int>>();
            for (int f = 0; f < faces; f++)
            {
                Vector3 a = positions[tris[f * 3]], b = positions[tris[f * 3 + 1]], c = positions[tris[f * 3 + 2]];
                faceNormal[f] = Vector3.Cross(b - a, c - a).normalized;
                cornerWeight[f * 3] = Vector3.Angle(b - a, c - a);
                cornerWeight[f * 3 + 1] = Vector3.Angle(c - b, a - b);
                cornerWeight[f * 3 + 2] = Vector3.Angle(a - c, b - c);
                for (int k = 0; k < 3; k++)
                {
                    List<int> corners;
                    if (!byPosition.TryGetValue(positions[tris[f * 3 + k]], out corners)) byPosition[positions[tris[f * 3 + k]]] = corners = new List<int>();
                    corners.Add(f * 3 + k);
                }
            }

            float cosCrease = Mathf.Cos(creaseAngle * Mathf.Deg2Rad);
            var result = new Vector3[positions.Length];
            for (int corner = 0; corner < tris.Length; corner++)
            {
                Vector3 own = faceNormal[corner / 3];
                Vector3 sum = Vector3.zero;
                foreach (int other in byPosition[positions[tris[corner]]])
                {
                    Vector3 n = faceNormal[other / 3];
                    if (Vector3.Dot(n, own) >= cosCrease) sum += n * cornerWeight[other];
                }
                result[tris[corner]] = sum.sqrMagnitude > 1e-12f ? sum.normalized : own;
            }
            return result;
        }

        static void ReverseWinding(int[] tris)
        {
            for (int t = 0; t < tris.Length; t += 3)
            {
                int tmp = tris[t + 1];
                tris[t + 1] = tris[t + 2];
                tris[t + 2] = tmp;
            }
        }

        static Matrix4x4 ColumnMajor(float[] m, int o)
        {
            var r = new Matrix4x4();
            for (int c = 0; c < 4; c++)
                for (int row = 0; row < 4; row++)
                    r[row, c] = m[o + c * 4 + row];
            return r;
        }

        static Matrix4x4 MirrorX(Matrix4x4 m)
        {
            Matrix4x4 s = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            return s * m * s;
        }

        static Transform SkeletonRoot(Transform[] bones)
        {
            var set = new HashSet<Transform>(bones);
            foreach (Transform b in bones)
                if (b.parent == null || !set.Contains(b.parent)) return b;
            return bones[0];
        }

        /// <summary>Mesh bounds expressed in the root bone's space, padded so bent fingers stay inside.</summary>
        static Bounds BoundsInBone(Mesh mesh, Matrix4x4 rootBindpose)
        {
            Vector3[] v = mesh.vertices;
            var b = new Bounds(rootBindpose.MultiplyPoint3x4(v[0]), Vector3.zero);
            for (int i = 1; i < v.Length; i++) b.Encapsulate(rootBindpose.MultiplyPoint3x4(v[i]));
            b.Expand(b.size.magnitude * 0.25f);
            return b;
        }

        // ------------------------------------------------------------------ data access

        sealed class Gltf
        {
            readonly Dictionary<string, object> _json;
            readonly byte[] _file;
            readonly int _bin;

            public Gltf(Dictionary<string, object> json, byte[] file, int bin)
            {
                _json = json;
                _file = file;
                _bin = bin;
            }

            public List<object> List(string key)
            {
                return _json.TryGetValue(key, out object v) ? (List<object>)v : new List<object>();
            }

            public float[] Floats(Dictionary<string, object> obj, string key)
            {
                if (!obj.TryGetValue(key, out object v)) return null;
                var list = (List<object>)v;
                var r = new float[list.Count];
                for (int i = 0; i < r.Length; i++) r[i] = Convert.ToSingle(list[i], CultureInfo.InvariantCulture);
                return r;
            }

            /// <summary>Reads an accessor as floats (integers converted; normalized types are not used by these files).</summary>
            public float[] Accessor(int index, out int components)
            {
                var a = (Dictionary<string, object>)List("accessors")[index];
                var view = (Dictionary<string, object>)List("bufferViews")[Convert.ToInt32(a["bufferView"])];
                int count = Convert.ToInt32(a["count"]);
                components = ComponentCount((string)a["type"]);
                int type = Convert.ToInt32(a["componentType"]);
                int size = type == 5126 || type == 5125 ? 4 : type == 5123 || type == 5122 ? 2 : 1;
                int offset = _bin + (view.TryGetValue("byteOffset", out object vo) ? Convert.ToInt32(vo) : 0)
                                  + (a.TryGetValue("byteOffset", out object ao) ? Convert.ToInt32(ao) : 0);
                int stride = view.TryGetValue("byteStride", out object st) ? Convert.ToInt32(st) : size * components;

                var r = new float[count * components];
                for (int e = 0; e < count; e++)
                    for (int c = 0; c < components; c++)
                    {
                        int at = offset + e * stride + c * size;
                        float val;
                        switch (type)
                        {
                            case 5126: val = BitConverter.ToSingle(_file, at); break;
                            case 5125: val = BitConverter.ToUInt32(_file, at); break;
                            case 5123: val = BitConverter.ToUInt16(_file, at); break;
                            case 5122: val = BitConverter.ToInt16(_file, at); break;
                            case 5121: val = _file[at]; break;
                            case 5120: val = (sbyte)_file[at]; break;
                            default: throw new NotSupportedException("componentType " + type);
                        }
                        r[e * components + c] = val;
                    }
                return r;
            }

            static int ComponentCount(string type)
            {
                switch (type)
                {
                    case "SCALAR": return 1;
                    case "VEC2": return 2;
                    case "VEC3": return 3;
                    case "VEC4": return 4;
                    case "MAT4": return 16;
                    default: throw new NotSupportedException("accessor type " + type);
                }
            }
        }

        /// <summary>Small JSON reader: objects, arrays, strings, numbers (double), bools, null.</summary>
        static class MiniJson
        {
            public static object Parse(string s)
            {
                int i = 0;
                return Value(s, ref i);
            }

            static void Skip(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object>();
                    i++;
                    Skip(s, ref i);
                    if (s[i] == '}') { i++; return d; }
                    while (true)
                    {
                        Skip(s, ref i);
                        string key = Str(s, ref i);
                        Skip(s, ref i);
                        i++; // ':'
                        d[key] = Value(s, ref i);
                        Skip(s, ref i);
                        if (s[i++] == '}') return d;
                    }
                }
                if (c == '[')
                {
                    var l = new List<object>();
                    i++;
                    Skip(s, ref i);
                    if (s[i] == ']') { i++; return l; }
                    while (true)
                    {
                        l.Add(Value(s, ref i));
                        Skip(s, ref i);
                        if (s[i++] == ']') return l;
                    }
                }
                if (c == '"') return Str(s, ref i);
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }

                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            static string Str(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++; // opening quote
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                    {
                        i++;
                        char e = s[i];
                        if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                        else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                    }
                    else sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }
        }
    }
}
