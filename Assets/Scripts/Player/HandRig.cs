using System.Collections.Generic;
using UnityEngine;

namespace Shields.Players
{
    public enum HandSide { Left, Right }

    /// <summary>The four digits of the cartoon hand (it has no little finger).</summary>
    public enum HandDigit { Thumb = 0, Index = 1, Middle = 2, Ring = 3 }

    /// <summary>
    /// Who is posing the fingers. A higher layer always wins over a lower one; when it is
    /// cleared the next one down shows again, and with nothing set the hand shows its rest
    /// pose. Required gameplay presentation (cards, the deck) is above held-object grips
    /// (weapon, mug), which are above emotes, so an emote can never corrupt a grip.
    /// </summary>
    public enum HandPoseLayer { Emote = 0, Grip = 1, Action = 2 }

    /// <summary>
    /// Semantic map and pose driver of one rigged hand: the final Blender hand
    /// (StylizedHand_Poses_FINAL, its mirrored _Left copy for the left hand). Lives on the
    /// root of the FinalHand_L/R prefab, inside the procedural hand wrapper (Hand_L / Hand_R),
    /// which keeps deciding WHERE the hand is. This decides only HOW it is posed there.
    ///
    /// Poses come from the Blender-authored Actions where one exists (rest, Point, Middle
    /// Finger, handshake), read once from the model's clips and stored here, so they need no
    /// Animator and work the same in a build. Gameplay poses without an authored Action are
    /// per-joint offsets from the bind pose (see <see cref="HandPoses"/>). Every change is a
    /// quaternion blend from where the bones are toward the target, so nothing snaps.
    ///
    /// The authored Actions also turn the whole hand (the Wrist bone) for display in Blender;
    /// that turn is not used, because where the floating hand points is the presenters' job.
    /// Presentation only.
    /// </summary>
    public sealed class HandRig : MonoBehaviour
    {
        const int LayerCount = 3;
        const int JointsPerDigit = 3;
        const int JointCount = 1 + 4 * JointsPerDigit;

        [SerializeField] HandSide side;
        [SerializeField] SkinnedMeshRenderer skin;

        [Tooltip("The pose this hand returns to when no layer poses it.")]
        [SerializeField] HandPoseId restPose = HandPoseId.Idle;

        [Tooltip("Root of the imported hand model (the clips' paths start here).")]
        [SerializeField] Transform model;

        [Tooltip("The Wrist bone: the palm. It turns about its head, the heel of the hand.")]
        [SerializeField] Transform wrist;

        [Tooltip("Base (knuckle) → tip for each digit; three bones each.")]
        [SerializeField] Transform[] thumb;
        [SerializeField] Transform[] index;
        [SerializeField] Transform[] middle;
        [SerializeField] Transform[] ring;

        [Tooltip("The model's authored Blender Actions. Read into Authored Poses by Capture Authored Poses.")]
        [SerializeField] AnimationClip[] authoredClips;

        [System.Serializable]
        public struct AuthoredPose
        {
            public string Name;
            [Tooltip("Local rotation of every mapped joint (wrist, then thumb, index, middle, ring).")]
            public Quaternion[] Rotations;
        }

        [SerializeField] AuthoredPose[] authoredPoses;

        // ---- captured from the imported bind pose (never from a posed hand)

        [SerializeField] Quaternion[] bindRotations;
        [SerializeField] Vector3[] curlAxes;
        [SerializeField] Vector3[] spreadAxes;
        [SerializeField] Vector3[] twistAxes;
        [SerializeField] Vector3 wristBindPosition;
        [Tooltip("Bind-pose finger direction and back-of-hand direction in this object's space.")]
        [SerializeField] Vector3 fingerAxisInRoot;
        [SerializeField] Vector3 backAxisInRoot;

        // ---- runtime pose state

        readonly HandPose[] _layers = new HandPose[LayerCount];
        HandPose _shown;
        Quaternion[] _from, _to;
        float _blend = 1f, _blendDuration;
        List<Transform> _joints;
        bool _reportedMissing;

        public HandSide Side { get { return side; } }
        public SkinnedMeshRenderer Skin { get { return skin; } }
        public Transform Wrist { get { return wrist; } }
        public HandPoseId RestPose { get { return restPose; } }
        public bool IsMapped { get { return wrist != null && Has(thumb) && Has(index) && Has(middle) && Has(ring); } }

        public bool HasBind
        {
            get
            {
                return bindRotations != null && bindRotations.Length == JointCount && curlAxes != null && curlAxes.Length == JointCount &&
                       spreadAxes != null && spreadAxes.Length == JointCount && twistAxes != null && twistAxes.Length == JointCount;
            }
        }

        /// <summary>The pose currently shown or being blended to.</summary>
        public HandPose ShownPose { get { return _shown; } }

        /// <summary>True while a blend between poses is running.</summary>
        public bool IsBlending { get { return _blend < 1f; } }

        /// <summary>The bones of one digit, base → tip.</summary>
        public Transform[] Chain(HandDigit digit)
        {
            switch (digit)
            {
                case HandDigit.Thumb: return thumb;
                case HandDigit.Index: return index;
                case HandDigit.Middle: return middle;
                default: return ring;
            }
        }

        /// <summary>
        /// The unposed hand's finger and back-of-hand directions in <paramref name="space"/>
        /// (for laying the hand flat on something). False when the rig is not mapped.
        /// </summary>
        public bool RestAxes(Transform space, out Vector3 fingers, out Vector3 back)
        {
            fingers = back = Vector3.zero;
            if (!HasBind || space == null) return false;
            fingers = space.InverseTransformDirection(transform.TransformDirection(fingerAxisInRoot)).normalized;
            back = space.InverseTransformDirection(transform.TransformDirection(backAxisInRoot)).normalized;
            return true;
        }

        /// <summary>True when an authored pose of that name (Blender Action, ".001"-style suffix ignored) was captured.</summary>
        public bool HasAuthored(string name)
        {
            return FindAuthored(name) >= 0;
        }

        /// <summary>Names of the poses <see cref="HandPoses"/> needs from this hand's authored Actions but it does not have.</summary>
        public List<string> MissingAuthoredPoses()
        {
            var missing = new List<string>();
            foreach (HandPoseId id in System.Enum.GetValues(typeof(HandPoseId)))
            {
                HandPose pose = HandPoses.Get(id, side);
                if (pose.Authored != null && !HasAuthored(pose.Authored) && !missing.Contains(pose.Authored)) missing.Add(pose.Authored);
            }

            return missing;
        }

        void Awake()
        {
            if (!HasBind && IsMapped) CaptureBind();
            ReportMissing();
            Show(0f);
        }

        void LateUpdate()
        {
            if (_blend >= 1f || _to == null) return;

            _blend = _blendDuration > 0f ? Mathf.Min(1f, _blend + Time.deltaTime / _blendDuration) : 1f;
            float e = _blend * _blend * (3f - 2f * _blend);

            List<Transform> joints = Joints();
            for (int i = 0; i < joints.Count; i++)
                if (joints[i] != null) joints[i].localRotation = Quaternion.Slerp(_from[i], _to[i], e);
        }

        // ------------------------------------------------------------------ pose API

        /// <summary>Poses the hand from <paramref name="layer"/>, blending over <paramref name="blend"/> seconds.</summary>
        public void SetPose(HandPoseLayer layer, HandPoseId pose, float blend)
        {
            SetPose(layer, HandPoses.Get(pose, side), blend);
        }

        /// <summary>Poses the hand from <paramref name="layer"/>; null clears that layer.</summary>
        public void SetPose(HandPoseLayer layer, HandPose pose, float blend)
        {
            _layers[(int)layer] = pose;
            Show(blend);
        }

        /// <summary>Gives the layer up; the next layer down (or the rest pose) shows again.</summary>
        public void ClearPose(HandPoseLayer layer, float blend)
        {
            if (_layers[(int)layer] == null) return;
            _layers[(int)layer] = null;
            Show(blend);
        }

        public bool IsPosedBy(HandPoseLayer layer) { return _layers[(int)layer] != null; }

        /// <summary>
        /// Drops every layer and returns to the rest pose (the authored Blender rest pose, not
        /// the raw imported bind pose). Zero blend restores it this instant.
        /// </summary>
        public void ResetToNeutral(float blend = 0f)
        {
            for (int i = 0; i < LayerCount; i++) _layers[i] = null;
            Show(blend);
        }

        /// <summary>Changes the rest pose (the waiter's tray hand rests holding the tray).</summary>
        public void SetRestPose(HandPoseId pose, float blend)
        {
            restPose = pose;
            Show(blend);
        }

        HandPose Active()
        {
            for (int i = LayerCount - 1; i >= 0; i--)
                if (_layers[i] != null) return _layers[i];
            return HandPoses.Get(restPose, side);
        }

        /// <summary>Starts a blend from wherever the bones are now to the active pose.</summary>
        void Show(float blend)
        {
            if (!HasBind || !IsMapped) return;

            HandPose target = Active();
            List<Transform> joints = Joints();
            int n = joints.Count;
            if (_from == null || _from.Length != n) { _from = new Quaternion[n]; _to = new Quaternion[n]; }

            for (int i = 0; i < n; i++) _from[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            Evaluate(target, _to);
            _shown = target;

            // The palm never moves inside the model: only rotations are posed.
            wrist.localPosition = wristBindPosition;

            if (blend <= 0f)
            {
                for (int i = 0; i < n; i++) if (joints[i] != null) joints[i].localRotation = _to[i];
                _blend = 1f;
                return;
            }

            _blendDuration = blend;
            _blend = 0f;
        }

        /// <summary>
        /// Local rotations of every mapped joint for a pose: its authored Action (or the bind
        /// pose) with the pose's per-joint offsets on top.
        /// </summary>
        public void Evaluate(HandPose pose, Quaternion[] rotations)
        {
            Quaternion[] source = bindRotations;
            if (pose != null && pose.Authored != null)
            {
                int a = FindAuthored(pose.Authored);
                if (a >= 0 && authoredPoses[a].Rotations != null && authoredPoses[a].Rotations.Length == JointCount) source = authoredPoses[a].Rotations;
            }

            for (int i = 0; i < JointCount; i++) rotations[i] = source[i];
            if (pose == null) return;

            rotations[0] = source[0] * Offset(0, pose.Wrist);
            int k = 1;
            for (int d = 0; d < 4; d++)
            {
                DigitPose digit = pose.Digit((HandDigit)d);
                for (int j = 0; j < JointsPerDigit; j++, k++)
                    rotations[k] = source[k] * Offset(k, digit.Joint(j));
            }
        }

        /// <summary>x = curl toward the palm, y = spread toward the thumb side, z = twist (palm face toward the thumb side).</summary>
        Quaternion Offset(int i, Vector3 degrees)
        {
            if (degrees == Vector3.zero) return Quaternion.identity;
            return Quaternion.AngleAxis(degrees.x, curlAxes[i]) *
                   Quaternion.AngleAxis(degrees.y, spreadAxes[i]) *
                   Quaternion.AngleAxis(degrees.z, twistAxes[i]);
        }

        int FindAuthored(string name)
        {
            if (authoredPoses == null || string.IsNullOrEmpty(name)) return -1;
            string key = BaseName(name);
            for (int i = 0; i < authoredPoses.Length; i++)
                if (BaseName(authoredPoses[i].Name) == key) return i;
            return -1;
        }

        /// <summary>"HandRig_L|Rest_Default.001" → "Rest_Default": Blender's object prefix and duplicate suffix are not part of the name.</summary>
        public static string BaseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int bar = name.LastIndexOf('|');
            if (bar >= 0) name = name.Substring(bar + 1);
            int dot = name.LastIndexOf('.');
            if (dot > 0 && dot < name.Length - 1)
            {
                bool digits = true;
                for (int i = dot + 1; i < name.Length; i++) digits &= char.IsDigit(name[i]);
                if (digits) name = name.Substring(0, dot);
            }

            return name;
        }

        void ReportMissing()
        {
            if (_reportedMissing || !IsMapped) return;
            _reportedMissing = true;
            List<string> missing = MissingAuthoredPoses();
            if (missing.Count > 0)
                Debug.LogError("HandRig '" + name + "': authored hand pose(s) not found: " + string.Join(", ", missing) +
                               ". Run 'Capture Authored Poses' on the hand prefab.", this);
        }

        // ------------------------------------------------------------------ capture

        /// <summary>
        /// Stores the imported bind pose (from the skin's bind poses, so the bones may be posed
        /// while this runs) and, for every joint, the local axes that curl it toward the palm,
        /// spread it toward the thumb side and twist it. The axes come from the skeleton's
        /// geometry and this hand's side, so one pose number means the same motion on a left
        /// and a right hand.
        /// </summary>
        [ContextMenu("Capture Bind Pose")]
        public void CaptureBind()
        {
            if (!IsMapped || skin == null) return;
            _joints = null;

            Dictionary<Transform, Matrix4x4> bind = BindWorld();
            List<Transform> joints = Joints();
            int n = joints.Count;
            bindRotations = new Quaternion[n];
            curlAxes = new Vector3[n];
            spreadAxes = new Vector3[n];
            twistAxes = new Vector3[n];

            Vector3 heel = Pos(bind, wrist);
            Vector3 palm = (heel + Pos(bind, middle[0])) * 0.5f;
            Vector3 fingers = Vector3.zero;
            foreach (Transform[] ch in new[] { index, middle, ring }) fingers += (Pos(bind, ch[ch.Length - 1]) - palm).normalized;
            fingers.Normalize();
            Vector3 thumbSide = Vector3.ProjectOnPlane(Pos(bind, thumb[0]) - palm, fingers).normalized;
            Vector3 normal = Vector3.Cross(fingers, thumbSide).normalized;
            if (side == HandSide.Left) normal = -normal;

            for (int i = 0; i < n; i++)
            {
                Transform t = joints[i];
                Quaternion world = Rot(bind, t);
                Quaternion parent = t.parent != null && bind.ContainsKey(t.parent) ? Rot(bind, t.parent) : (t.parent != null ? t.parent.rotation : Quaternion.identity);
                bindRotations[i] = Quaternion.Inverse(parent) * world;

                Vector3 dir = i == 0 ? fingers
                    : t.childCount > 0 && bind.ContainsKey(t.GetChild(0)) ? (Pos(bind, t.GetChild(0)) - Pos(bind, t)).normalized
                    : (Pos(bind, t) - Pos(bind, t.parent)).normalized;

                Quaternion toLocal = Quaternion.Inverse(world);
                curlAxes[i] = (toLocal * Toward(dir, normal, Vector3.Cross(dir, normal))).normalized;
                spreadAxes[i] = (toLocal * Toward(dir, thumbSide, Vector3.Cross(dir, thumbSide))).normalized;
                twistAxes[i] = (toLocal * Toward(normal, thumbSide, dir)).normalized;
            }

            Matrix4x4 parentBind = bind.ContainsKey(wrist.parent) ? bind[wrist.parent] : wrist.parent.localToWorldMatrix;
            wristBindPosition = parentBind.inverse.MultiplyPoint3x4(heel);

            fingerAxisInRoot = transform.InverseTransformDirection(fingers).normalized;
            backAxisInRoot = transform.InverseTransformDirection(Vector3.ProjectOnPlane(-normal, fingers)).normalized;
        }

        /// <summary>World matrix of every skinned bone in the bind pose, from the mesh's bind poses.</summary>
        Dictionary<Transform, Matrix4x4> BindWorld()
        {
            var d = new Dictionary<Transform, Matrix4x4>();
            Matrix4x4[] poses = skin.sharedMesh.bindposes;
            Transform[] bones = skin.bones;
            for (int i = 0; i < bones.Length && i < poses.Length; i++)
                if (bones[i] != null) d[bones[i]] = skin.transform.localToWorldMatrix * poses[i].inverse;
            return d;
        }

        static Vector3 Pos(Dictionary<Transform, Matrix4x4> bind, Transform t)
        {
            Matrix4x4 m;
            return bind.TryGetValue(t, out m) ? (Vector3)m.GetColumn(3) : t.position;
        }

        static Quaternion Rot(Dictionary<Transform, Matrix4x4> bind, Transform t)
        {
            Matrix4x4 m;
            if (!bind.TryGetValue(t, out m)) return t.rotation;
            return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        }

        /// <summary>The sign of <paramref name="axis"/> that swings <paramref name="from"/> toward <paramref name="toward"/>.</summary>
        static Vector3 Toward(Vector3 from, Vector3 toward, Vector3 axis)
        {
            if (axis.sqrMagnitude < 1e-12f) axis = Vector3.Cross(from, Vector3.up);
            axis.Normalize();
            return Vector3.Dot(Quaternion.AngleAxis(5f, axis) * from, toward) >= Vector3.Dot(from, toward) ? axis : -axis;
        }

        /// <summary>
        /// Fills the map from the final hand skeleton's bone names: Wrist (the palm), and
        /// Thumb_, Index_, Middle_ and Outer_ (the ring finger) 01 → 03, then captures the
        /// bind pose. Run on the prefab after the model is placed.
        /// </summary>
        [ContextMenu("Map Final Hand Skeleton")]
        public void AutoMap()
        {
            if (skin == null) skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null) return;
            if (model == null)
            {
                Animator animator = GetComponentInChildren<Animator>(true);
                model = animator != null ? animator.transform : skin.transform.parent;
            }

            wrist = FindBone("Wrist");
            thumb = Digit("Thumb");
            index = Digit("Index");
            middle = Digit("Middle");
            ring = Digit("Outer");
            _joints = null;

            if (!IsMapped) { Debug.LogWarning("HandRig: the final hand skeleton bones were not all found.", this); return; }
            CaptureBind();
        }

        Transform[] Digit(string prefix)
        {
            var chain = new Transform[JointsPerDigit];
            for (int i = 0; i < JointsPerDigit; i++) chain[i] = FindBone(prefix + "_0" + (i + 1));
            return chain;
        }

        Transform FindBone(string bone)
        {
            foreach (Transform t in skin.bones)
                if (t != null && t.name == bone) return t;
            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Reads every authored Action's joint rotations (first frame) from the clips into
        /// <see cref="authoredPoses"/>. The Wrist keeps its bind rotation: the Actions turn the
        /// whole hand only for display in Blender. Run again after re-exporting the model.
        /// </summary>
        [ContextMenu("Capture Authored Poses")]
        public void CaptureAuthoredPoses()
        {
            if (!HasBind || model == null || authoredClips == null) return;
            _joints = null;
            List<Transform> joints = Joints();
            var poses = new List<AuthoredPose>();

            foreach (AnimationClip clip in authoredClips)
            {
                if (clip == null) continue;
                var rotations = new Quaternion[JointCount];
                for (int i = 0; i < JointCount; i++)
                {
                    rotations[i] = bindRotations[i];
                    if (i == 0) continue;
                    string path = UnityEditor.AnimationUtility.CalculateTransformPath(joints[i], model);
                    Vector4 q;
                    if (Curve(clip, path, out q)) rotations[i] = new Quaternion(q.x, q.y, q.z, q.w).normalized;
                }

                poses.Add(new AuthoredPose { Name = BaseName(clip.name), Rotations = rotations });
            }

            authoredPoses = poses.ToArray();
        }

        static bool Curve(AnimationClip clip, string path, out Vector4 q)
        {
            q = Vector4.zero;
            string[] parts = { "x", "y", "z", "w" };
            for (int c = 0; c < 4; c++)
            {
                var binding = UnityEditor.EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + parts[c]);
                AnimationCurve curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) return false;
                q[c] = curve.Evaluate(0f);
            }

            return true;
        }

        public void SetAuthoredClips(AnimationClip[] clips) { authoredClips = clips; }
#endif

        public void SetSide(HandSide value) { side = value; }

        // ------------------------------------------------------------------ helpers

        List<Transform> Joints()
        {
            if (_joints != null && Application.isPlaying) return _joints;
            var list = new List<Transform> { wrist };
            AddAll(list, thumb);
            AddAll(list, index);
            AddAll(list, middle);
            AddAll(list, ring);
            _joints = list;
            return list;
        }

        static void AddAll(List<Transform> list, Transform[] items)
        {
            if (items != null) list.AddRange(items);
        }

        static bool Has(Transform[] a)
        {
            if (a == null || a.Length != JointsPerDigit) return false;
            foreach (Transform t in a) if (t == null) return false;
            return true;
        }
    }
}
