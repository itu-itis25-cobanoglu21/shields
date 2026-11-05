using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shields.EditorTools
{
    /// <summary>
    /// Import-time repair of the auto-rigged hand's skin weights (the source file is never touched).
    ///
    /// The cartoon fingers are separate tubes that touch their neighbours in the bind pose, and the
    /// auto-rigger let weight leak across: whole strips along the side of a finger follow the
    /// NEIGHBOURING finger's bones, so curling one finger on its own tears that strip off into spikes.
    /// Which finger a vertex really lies on is found from the mesh itself (a flood over the surface
    /// from every fingertip), not from its strongest bone, which is wrong exactly on those strips. Then:
    ///  1. weight on other fingers' bones goes to the vertex's own finger, fully out on the finger and
    ///     fading to nothing toward the crotch where the fingers meet the palm;
    ///  2. how each finger vertex splits its weight along its own finger's bones (and the palm and
    ///     wrist above them) is lightly averaged with its neighbours on the same finger, evening out the
    ///     knuckle transitions. The share that follows the heel or another finger is kept.
    /// Palm vertices behind the crotch line are never changed. Every result keeps at most four
    /// influences that sum to one. Meshes without a hand-like skeleton are left alone.
    /// </summary>
    static class FingerWeights
    {
        // Fractions of each finger's own length, measured over the surface from its tip to its crotch.
        const float CleanupFade = 0.25f;  // the cross-finger cleanup fades out over this last part before the crotch
        const float SmoothReach = 0.125f; // the smoothing also reaches this far behind the crotch, over the knuckle
        const float TipSeedWeight = 0.3f; // a fingertip vertex must carry at least this much of its tip bone

        /// <summary>Repairs the mesh's bone weights in place. Returns how many vertex positions changed.</summary>
        public static int Fix(Mesh mesh, Transform[] bones, float cleanup, float smoothing, int passes)
        {
            BoneWeight[] weights = mesh.boneWeights;
            int boneCount = bones.Length;
            if (weights.Length == 0 || boneCount < 4) return 0;

            // ---- skeleton: the palm is the bone with the most child bones, the fingers its chains minus the heel

            var boneIndex = new Dictionary<Transform, int>();
            for (int b = 0; b < boneCount; b++) boneIndex[bones[b]] = b;

            int palm = -1, most = 0;
            for (int b = 0; b < boneCount; b++)
            {
                int count = 0;
                foreach (Transform child in bones[b]) if (boneIndex.ContainsKey(child)) count++;
                if (count > most) { most = count; palm = b; }
            }
            if (palm < 0 || most < 3) return 0;

            Matrix4x4[] bindposes = mesh.bindposes;
            var joint = new Vector3[boneCount];
            for (int b = 0; b < boneCount; b++) joint[b] = bindposes[b].inverse.GetColumn(3);

            var chains = new List<List<int>>();
            foreach (Transform child in bones[palm])
            {
                if (!boneIndex.ContainsKey(child)) continue;
                var chain = new List<int>();
                Transform t = child;
                while (t != null)
                {
                    chain.Add(boneIndex[t]);
                    Transform next = null;
                    foreach (Transform c in t) if (boneIndex.ContainsKey(c)) { next = c; break; }
                    t = next;
                }
                chains.Add(chain);
            }

            // The heel is the chain pointing most against the others (as HandRig.AutoMap decides).
            Vector3 mean = Vector3.zero;
            foreach (List<int> c in chains) mean += (joint[c[c.Count - 1]] - joint[palm]).normalized;
            int heel = 0;
            float lowest = float.MaxValue;
            for (int k = 0; k < chains.Count; k++)
            {
                float d = Vector3.Dot((joint[chains[k][chains[k].Count - 1]] - joint[palm]).normalized, mean);
                if (d < lowest) { lowest = d; heel = k; }
            }
            chains.RemoveAt(heel);
            int fingers = chains.Count;
            if (fingers < 2) return 0;

            var fingerOf = new int[boneCount];
            for (int b = 0; b < boneCount; b++) fingerOf[b] = -1;
            for (int k = 0; k < fingers; k++) foreach (int b in chains[k]) fingerOf[b] = k;

            // The palm and the bones above it (the wrist) share every finger's chain for smoothing.
            var above = new bool[boneCount];
            for (Transform t = bones[palm]; t != null && boneIndex.ContainsKey(t); t = t.parent) above[boneIndex[t]] = true;

            // ---- surface: one node per position (the mesh is split per face), edges from the triangles

            Vector3[] positions = mesh.vertices;
            int[] tris = mesh.triangles;
            var ids = new Dictionary<Vector3, int>();
            var weld = new int[positions.Length];
            var first = new List<int>();
            for (int v = 0; v < positions.Length; v++)
            {
                if (!ids.TryGetValue(positions[v], out int id)) { id = first.Count; ids[positions[v]] = id; first.Add(v); }
                weld[v] = id;
            }
            int n = first.Count;

            var sets = new HashSet<int>[n];
            for (int i = 0; i < n; i++) sets[i] = new HashSet<int>();
            for (int t = 0; t < tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = weld[tris[t + k]], b = weld[tris[t + (k + 1) % 3]];
                    if (a == b) continue;
                    sets[a].Add(b);
                    sets[b].Add(a);
                }
            var neighbours = new int[n][];
            for (int i = 0; i < n; i++) { neighbours[i] = new int[sets[i].Count]; sets[i].CopyTo(neighbours[i]); }

            var w = new float[n, boneCount];
            for (int i = 0; i < n; i++)
            {
                BoneWeight bw = weights[first[i]];
                w[i, bw.boneIndex0] += bw.weight0;
                w[i, bw.boneIndex1] += bw.weight1;
                w[i, bw.boneIndex2] += bw.weight2;
                w[i, bw.boneIndex3] += bw.weight3;
            }

            // ---- which finger each vertex lies on: the nearest fingertip, measured over the surface

            var dist = new float[n];
            var label = new int[n];
            for (int i = 0; i < n; i++) { dist[i] = float.MaxValue; label[i] = -1; }
            var queue = new SortedSet<(float, int)>();
            for (int k = 0; k < fingers; k++)
            {
                List<int> chain = chains[k];
                int tip = chain[chain.Count - 1];
                Vector3 from = chain.Count > 1 ? joint[chain[chain.Count - 2]] : joint[palm];
                Vector3 dir = (joint[tip] - from).normalized;
                int apex = -1;
                float far = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    if (w[i, tip] < TipSeedWeight) continue;
                    Vector3 p = positions[first[i]] - joint[tip];
                    float score = Vector3.Dot(p, dir) - Vector3.ProjectOnPlane(p, dir).magnitude;
                    if (score > far) { far = score; apex = i; }
                }
                if (apex < 0 || label[apex] >= 0) continue;
                dist[apex] = 0f;
                label[apex] = k;
                queue.Add((0f, apex));
            }

            while (queue.Count > 0)
            {
                (float d, int v) = queue.Min;
                queue.Remove(queue.Min);
                foreach (int j in neighbours[v])
                {
                    float nd = d + (positions[first[v]] - positions[first[j]]).magnitude;
                    if (nd >= dist[j]) continue;
                    if (dist[j] < float.MaxValue) queue.Remove((dist[j], j));
                    dist[j] = nd;
                    label[j] = label[v];
                    queue.Add((nd, j));
                }
            }

            // Each finger's crotch: where its flood front first meets another finger's, along an edge.
            var crotch = new float[fingers];
            for (int k = 0; k < fingers; k++) crotch[k] = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (label[i] < 0) continue;
                foreach (int j in neighbours[i])
                {
                    if (label[j] < 0 || label[j] == label[i]) continue;
                    float meet = (dist[i] + (positions[first[i]] - positions[first[j]]).magnitude + dist[j]) * 0.5f;
                    if (meet < crotch[label[i]]) crotch[label[i]] = meet;
                }
            }

            var onFinger = new float[n];  // 1 out on the finger, 0 from the crotch back
            var reach = new float[n];     // the same, shifted a little past the crotch for the smoothing
            for (int i = 0; i < n; i++)
            {
                int k = label[i];
                if (k < 0 || crotch[k] == float.MaxValue) continue;
                float fade = CleanupFade * crotch[k];
                onFinger[i] = SmoothStep((crotch[k] - dist[i]) / fade);
                reach[i] = SmoothStep((crotch[k] - dist[i] + SmoothReach * crotch[k]) / fade);
            }

            var changed = new bool[n];

            // ---- 1. weight on other fingers' bones goes to the finger the vertex lies on

            if (cleanup > 0f)
                for (int i = 0; i < n; i++)
                {
                    int k = label[i];
                    if (k < 0 || onFinger[i] <= 0f) continue;

                    float own = 0f, moved = 0f;
                    for (int b = 0; b < boneCount; b++)
                    {
                        if (fingerOf[b] == k) own += w[i, b];
                        else if (fingerOf[b] >= 0 && w[i, b] > 0f)
                        {
                            float m = w[i, b] * cleanup * onFinger[i];
                            w[i, b] -= m;
                            moved += m;
                        }
                    }
                    if (moved <= 0f) continue;

                    if (own > 1e-4f)
                    {
                        for (int b = 0; b < boneCount; b++) if (fingerOf[b] == k) w[i, b] += moved * w[i, b] / own;
                    }
                    else
                    {
                        int nearest = chains[k][0];
                        float best = float.MaxValue;
                        foreach (int b in chains[k])
                        {
                            float d = (joint[b] - positions[first[i]]).sqrMagnitude;
                            if (d < best) { best = d; nearest = b; }
                        }
                        w[i, nearest] += moved;
                    }
                    changed[i] = true;
                }

            // ---- 2. even out how finger vertices split their weight along their own finger

            if (smoothing > 0f && passes > 0)
            {
                var share = new float[n];          // weight on the vertex's own finger, palm and wrist: kept as is
                var split = new float[n, boneCount]; // how that share is split: smoothed
                for (int i = 0; i < n; i++)
                {
                    int k = label[i];
                    if (k < 0 || reach[i] <= 0f) continue;
                    for (int b = 0; b < boneCount; b++) if (above[b] || fingerOf[b] == k) share[i] += w[i, b];
                    if (share[i] <= 1e-4f) { reach[i] = 0f; continue; }
                    for (int b = 0; b < boneCount; b++) if (above[b] || fingerOf[b] == k) split[i, b] = w[i, b] / share[i];
                }

                var next = new float[n, boneCount];
                for (int pass = 0; pass < passes; pass++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int count = 0;
                        if (reach[i] > 0f)
                            foreach (int j in neighbours[i])
                                if (reach[j] > 0f && label[j] == label[i])
                                {
                                    count++;
                                    for (int b = 0; b < boneCount; b++) next[i, b] += split[j, b];
                                }

                        float s = smoothing * reach[i];
                        for (int b = 0; b < boneCount; b++)
                            next[i, b] = count > 0 ? (1f - s) * split[i, b] + s * next[i, b] / count : split[i, b];
                    }
                    float[,] swap = split; split = next; next = swap;
                    Array.Clear(next, 0, next.Length);
                }

                for (int i = 0; i < n; i++)
                {
                    int k = label[i];
                    if (k < 0 || reach[i] <= 0f) continue;
                    for (int b = 0; b < boneCount; b++) if (above[b] || fingerOf[b] == k) w[i, b] = share[i] * split[i, b];
                    changed[i] = true;
                }
            }

            // ---- back to at most four influences, renormalised, on every copy of each changed position

            int changedCount = 0;
            var top = new int[4];
            var fixedWeights = new BoneWeight[n];
            for (int i = 0; i < n; i++)
            {
                if (!changed[i]) continue;
                for (int slot = 0; slot < 4; slot++)
                {
                    int pick = -1;
                    for (int b = 0; b < boneCount; b++)
                    {
                        bool taken = false;
                        for (int s = 0; s < slot; s++) if (top[s] == b) taken = true;
                        if (!taken && (pick < 0 || w[i, b] > w[i, pick])) pick = b;
                    }
                    top[slot] = pick;
                }
                float sum = 0f;
                for (int slot = 0; slot < 4; slot++) sum += Mathf.Max(0f, w[i, top[slot]]);
                float scale = sum > 0f ? 1f / sum : 0f;
                var result = new BoneWeight
                {
                    boneIndex0 = top[0], weight0 = Mathf.Max(0f, w[i, top[0]]) * scale,
                    boneIndex1 = top[1], weight1 = Mathf.Max(0f, w[i, top[1]]) * scale,
                    boneIndex2 = top[2], weight2 = Mathf.Max(0f, w[i, top[2]]) * scale,
                    boneIndex3 = top[3], weight3 = Mathf.Max(0f, w[i, top[3]]) * scale,
                };
                if (sum <= 0f) { changed[i] = false; continue; }
                fixedWeights[i] = result;
                changedCount++;
            }

            for (int v = 0; v < weights.Length; v++)
                if (changed[weld[v]]) weights[v] = fixedWeights[weld[v]];

            mesh.boneWeights = weights;
            return changedCount;
        }

        static float SmoothStep(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
