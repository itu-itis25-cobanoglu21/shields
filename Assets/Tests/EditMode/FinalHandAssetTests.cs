using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Shields.Cards.Tests
{
    /// <summary>
    /// The final Blender hands: the Left FBX must be an exact mirror of the Right one (mesh
    /// rig and every authored Action), with positive scale everywhere, and the characters must
    /// use the FinalHand prefabs built from them.
    /// </summary>
    public class FinalHandAssetTests
    {
        const string RightFbx = "Assets/Art/Characters/hands/StylizedHand_Poses_FINAL.fbx";
        const string LeftFbx = "Assets/Art/Characters/hands/StylizedHand_Poses_FINAL_Left.fbx";
        const string RightPrefab = "Assets/Prefabs/Hands/FinalHand_R.prefab";
        const string LeftPrefab = "Assets/Prefabs/Hands/FinalHand_L.prefab";

        static readonly string[] Bones =
        {
            "Root", "Wrist", "Thumb_01", "Thumb_02", "Thumb_03", "Index_01", "Index_02", "Index_03",
            "Middle_01", "Middle_02", "Middle_03", "Outer_01", "Outer_02", "Outer_03"
        };

        static readonly string[] Actions = { "Rest_Default", "Pose_Point", "Pose_MiddleFinger", "Pose_HandshakeReady", "Pose_ThumbsUp" };

        static Dictionary<string, AnimationClip> Clips(string path)
        {
            var d = new Dictionary<string, AnimationClip>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview")) d[c.name.Split('|').Last().Split('.')[0]] = c;
            }
            return d;
        }

        static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        [Test]
        public void BothHandsHaveTheSameRigAndActions()
        {
            var right = AssetDatabase.LoadAssetAtPath<GameObject>(RightFbx);
            var left = AssetDatabase.LoadAssetAtPath<GameObject>(LeftFbx);
            Assert.IsNotNull(right, "final right hand FBX missing");
            Assert.IsNotNull(left, "mirrored left hand FBX missing");

            var rs = right.GetComponentInChildren<SkinnedMeshRenderer>();
            var ls = left.GetComponentInChildren<SkinnedMeshRenderer>();
            CollectionAssert.AreEquivalent(Bones, rs.bones.Select(b => b.name).ToArray());
            CollectionAssert.AreEquivalent(Bones, ls.bones.Select(b => b.name).ToArray());
            Assert.AreEqual(rs.sharedMesh.GetIndexCount(0), ls.sharedMesh.GetIndexCount(0));

            var rc = Clips(RightFbx);
            var lc = Clips(LeftFbx);
            foreach (string a in Actions)
            {
                Assert.IsTrue(rc.ContainsKey(a), "right hand lacks Action " + a);
                Assert.IsTrue(lc.ContainsKey(a), "left hand lacks Action " + a);
            }
        }

        [Test]
        public void LeftHandIsTheMirroredRightInEveryAction()
        {
            var right = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RightFbx));
            var left = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LeftFbx));
            try
            {
                var rc = Clips(RightFbx);
                var lc = Clips(LeftFbx);
                foreach (string a in Actions)
                {
                    rc[a].SampleAnimation(right, 0f);
                    lc[a].SampleAnimation(left, 0f);
                    foreach (string b in Bones)
                    {
                        Transform rb = Find(right.transform, b), lb = Find(left.transform, b);
                        foreach (Vector3 local in new[] { Vector3.zero, Vector3.up * 0.0002f })
                        {
                            Vector3 r = right.transform.InverseTransformPoint(rb.TransformPoint(local));
                            Vector3 l = left.transform.InverseTransformPoint(lb.TransformPoint(local));
                            r.x = -r.x;
                            Assert.Less((r - l).magnitude, 1e-4f, a + " / " + b + " is not mirrored");
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(right);
                Object.DestroyImmediate(left);
            }
        }

        [Test]
        public void LeftAndRightDefaultPosesAreMirrored()
        {
            // The pose a model shows before anything animates it (Project previews, prefabs in
            // edit mode) comes from the FBX's first take; both hands must show their rest pose.
            var right = AssetDatabase.LoadAssetAtPath<GameObject>(RightFbx).transform;
            var left = AssetDatabase.LoadAssetAtPath<GameObject>(LeftFbx).transform;
            foreach (string b in Bones)
            {
                Transform rb = Find(right, b), lb = Find(left, b);
                foreach (Vector3 local in new[] { Vector3.zero, Vector3.up * 0.0002f })
                {
                    Vector3 r = right.InverseTransformPoint(rb.TransformPoint(local));
                    Vector3 l = left.InverseTransformPoint(lb.TransformPoint(local));
                    r.x = -r.x;
                    Assert.Less((r - l).magnitude, 1e-4f, "default pose of " + b + " is not mirrored");
                }
            }
        }

        [Test]
        public void FinalHandsHavePositiveScaleAndAuthoredPoses()
        {
            foreach (string path in new[] { RightFbx, LeftFbx, RightPrefab, LeftPrefab })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(go, path + " missing");
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                    Assert.IsTrue(t.localScale.x > 0f && t.localScale.y > 0f && t.localScale.z > 0f, path + ": negative scale on " + t.name);
            }

            foreach (string path in new[] { RightPrefab, LeftPrefab })
            {
                var rig = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent("HandRig");
                Assert.IsNotNull(rig, path + " has no HandRig");
                var poses = new SerializedObject(rig).FindProperty("authoredPoses");
                var names = new List<string>();
                for (int i = 0; i < poses.arraySize; i++)
                {
                    var p = poses.GetArrayElementAtIndex(i);
                    names.Add(p.FindPropertyRelative("Name").stringValue);
                    Assert.AreEqual(13, p.FindPropertyRelative("Rotations").arraySize, path + " pose " + names.Last() + " is incomplete");
                }
                foreach (string a in Actions) CollectionAssert.Contains(names, a, path + " lacks authored pose " + a);
            }
        }

        [Test]
        public void CharactersUseTheFinalHands()
        {
            foreach (string path in new[] { "Assets/Prefabs/PlayerCharacter.prefab", "Assets/Prefabs/Tavern/Waiter.prefab" })
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var hands = root.GetComponentsInChildren<Transform>(true)
                    .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    .Select(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject))
                    .Where(p => p.StartsWith("Assets/Prefabs/Hands/"))
                    .ToList();
                CollectionAssert.AreEquivalent(new[] { LeftPrefab, RightPrefab }, hands, path + " hands");
            }
        }
    }
}
