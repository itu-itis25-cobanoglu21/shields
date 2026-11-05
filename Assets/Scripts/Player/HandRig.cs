using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Shields.Players
{
    public enum HandSide { Left, Right }

    /// <summary>The four digits of the cartoon hand (it has no little finger).</summary>
    public enum HandDigit { Thumb = 0, Index = 1, Middle = 2, Ring = 3 }

    /// <summary>
    /// Who is posing the fingers. A higher layer always wins over a lower one; when it is
    /// cleared the next one down shows again, and with nothing set the hand shows its rest
    /// pose. Required gameplay presentation (cards, the deck) is above held-object grips
    /// (weapon, mug), which are above future emotes, so an emote can never corrupt a grip.
    /// </summary>
    public enum HandPoseLayer { Emote = 0, Grip = 1, Action = 2 }

    /// <summary>
    /// Semantic map and pose driver of one rigged cartoon hand. Lives on the root of the
    /// RiggedHand_L/R prefab, inside the procedural hand wrapper (Hand_L / Hand_R), which
    /// keeps deciding WHERE the hand is. This decides only HOW it is posed there: the wrist
    /// angle and the finger joints, always relative to the raw imported bind pose and always
    /// blended (quaternion slerp) toward a known target pose, so nothing drifts or snaps.
    /// Presentation only.
    /// </summary>
    public sealed class HandRig : MonoBehaviour
    {
        const int LayerCount = 3;

        [SerializeField] HandSide side;
        [SerializeField] SkinnedMeshRenderer skin;

        [Tooltip("The pose this hand returns to when no layer poses it.")]
        [SerializeField] HandPoseId restPose = HandPoseId.Idle;

        [Tooltip("Skeleton root at the middle of the palm; turning it turns the whole hand mesh.")]
        [SerializeField] Transform wrist;
        [SerializeField] Transform palm;
        [Tooltip("Heel of the hand (the end that would meet the wrist).")]
        [SerializeField] Transform[] heel;

        [Tooltip("Base → tip for each digit.")]
        [SerializeField] Transform[] thumb;
        [SerializeField] Transform[] index;
        [SerializeField] Transform[] middle;
        [SerializeField] Transform[] ring;

        // ---- captured from the imported bind pose (never from a posed hand)

        [FormerlySerializedAs("neutralRotations")]
        [SerializeField] Quaternion[] bindRotations;
        [SerializeField] Vector3[] curlAxes;
        [SerializeField] Vector3[] spreadAxes;
        [SerializeField] Vector3[] twistAxes;
        [SerializeField] Vector3 wristBindPosition;
        [SerializeField] Vector3 wristPivot;
        [Tooltip("Rest finger direction and back-of-hand direction in this object's space.")]
        [SerializeField] Vector3 fingerAxisInRoot;
        [SerializeField] Vector3 backAxisInRoot;

        // ---- runtime pose state

        readonly HandPose[] _layers = new HandPose[LayerCount];
        HandPose _shown;
        Quaternion[] _from, _to;
        Vector3 _fromWrist, _toWrist;
        float _blend = 1f, _blendDuration;
        List<Transform> _joints;

        public HandSide Side { get { return side; } }
        public SkinnedMeshRenderer Skin { get { return skin; } }
        public Transform Wrist { get { return wrist; } }
        public Transform Palm { get { return palm; } }
        public HandPoseId RestPose { get { return restPose; } }
        public bool IsMapped { get { return wrist != null && palm != null && Has(thumb) && Has(index) && Has(middle) && Has(ring); } }
        public bool HasBind { get { return bindRotations != null && bindRotations.Length > 0 && bindRotations.Length == Joints().Count && spreadAxes != null && spreadAxes.Length == bindRotations.Length; } }

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

        void Awake()
        {
            if (!HasBind && IsMapped) CaptureBind();
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
            if (wrist != null) wrist.localPosition = Vector3.Lerp(_fromWrist, _toWrist, e);
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
        /// Drops every layer and returns to the rest pose (the polished idle pose, not the raw
        /// imported bind pose). Zero blend restores it this instant.
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
            if (!HasBind) return;

            HandPose target = Active();
            List<Transform> joints = Joints();
            int n = joints.Count;
            if (_from == null || _from.Length != n) { _from = new Quaternion[n]; _to = new Quaternion[n]; }

            for (int i = 0; i < n; i++) _from[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            _fromWrist = wrist != null ? wrist.localPosition : Vector3.zero;

            Evaluate(target, _to, out _toWrist);
            _shown = target;

            if (blend <= 0f)
            {
                for (int i = 0; i < n; i++) if (joints[i] != null) joints[i].localRotation = _to[i];
                if (wrist != null) wrist.localPosition = _toWrist;
                _blend = 1f;
                return;
            }

            _blendDuration = blend;
            _blend = 0f;
        }

        /// <summary>Local rotations of every mapped bone for a pose (bind pose plus its offsets).</summary>
        public void Evaluate(HandPose pose, Quaternion[] rotations, out Vector3 wristPosition)
        {
            List<Transform> joints = Joints();
            for (int i = 0; i < joints.Count; i++) rotations[i] = bindRotations[i];
            wristPosition = wristBindPosition;
            if (pose == null) return;

            // Wrist: turned about the heel of the hand, not the middle of the palm.
            Quaternion w = Offset(0, pose.Wrist);
            rotations[0] = bindRotations[0] * w;
            Quaternion inParent = bindRotations[0] * w * Quaternion.Inverse(bindRotations[0]);
            wristPosition = wristPivot + inParent * (wristBindPosition - wristPivot);

            int offset = 2 + Len(heel);
            for (int d = 0; d < 4; d++)
            {
                Transform[] chain = Chain((HandDigit)d);
                DigitPose digit = pose.Digit((HandDigit)d);
                for (int j = 0; j < chain.Length; j++)
                    rotations[offset + j] = bindRotations[offset + j] * Offset(offset + j, digit.Joint(j));
                offset += chain.Length;
            }
        }

        /// <summary>x = curl toward the palm, y = spread toward the thumb side, z = twist (palm face toward the thumb side).</summary>
        Quaternion Offset(int i, Vector3 degrees)
        {
            return Quaternion.AngleAxis(degrees.x, curlAxes[i]) *
                   Quaternion.AngleAxis(degrees.y, spreadAxes[i]) *
                   Quaternion.AngleAxis(degrees.z, twistAxes[i]);
        }

        // ------------------------------------------------------------------ capture

        /// <summary>
        /// Stores the imported bind pose and, for every joint, the local axes that curl it
        /// toward the palm, spread it toward the thumb side and twist it. The axes come from
        /// the skeleton's geometry and this hand's side, so one pose number means the same
        /// motion on a left and a right hand. Must run on the unposed hand.
        /// </summary>
        [ContextMenu("Capture Bind Pose")]
        public void CaptureBind()
        {
            List<Transform> joints = Joints();
            int n = joints.Count;
            bindRotations = new Quaternion[n];
            curlAxes = new Vector3[n];
            spreadAxes = new Vector3[n];
            twistAxes = new Vector3[n];

            Vector3 fingers = Vector3.zero;
            foreach (Transform[] ch in new[] { index, middle, ring }) fingers += (Tip(ch) - palm.position).normalized;
            fingers.Normalize();
            Vector3 thumbSide = Vector3.ProjectOnPlane(thumb[0].position - palm.position, fingers).normalized;
            Vector3 normal = Vector3.Cross(fingers, thumbSide).normalized;
            if (side == HandSide.Left) normal = -normal;

            for (int i = 0; i < n; i++)
            {
                Transform t = joints[i];
                if (t == null) continue;
                bindRotations[i] = t.localRotation;

                Vector3 dir = i == 0 ? fingers
                    : t.childCount > 0 ? (t.GetChild(0).position - t.position).normalized
                    : (t.position - t.parent.position).normalized;
                curlAxes[i] = t.InverseTransformDirection(Toward(dir, normal, Vector3.Cross(dir, normal))).normalized;
                spreadAxes[i] = t.InverseTransformDirection(Toward(dir, thumbSide, Vector3.Cross(dir, thumbSide))).normalized;
                twistAxes[i] = t.InverseTransformDirection(Toward(normal, thumbSide, dir)).normalized;
            }

            wristBindPosition = wrist.localPosition;
            Transform heelEnd = heel != null && heel.Length > 0 ? heel[heel.Length - 1] : palm;
            wristPivot = wrist.parent != null ? wrist.parent.InverseTransformPoint(heelEnd.position) : heelEnd.position;

            fingerAxisInRoot = transform.InverseTransformDirection(fingers).normalized;
            backAxisInRoot = transform.InverseTransformDirection(Vector3.ProjectOnPlane(-normal, fingers)).normalized;
        }

        /// <summary>The sign of <paramref name="axis"/> that swings <paramref name="from"/> toward <paramref name="toward"/>.</summary>
        static Vector3 Toward(Vector3 from, Vector3 toward, Vector3 axis)
        {
            if (axis.sqrMagnitude < 1e-12f) axis = Vector3.Cross(from, Vector3.up);
            axis.Normalize();
            return Vector3.Dot(Quaternion.AngleAxis(5f, axis) * from, toward) >= Vector3.Dot(from, toward) ? axis : -axis;
        }

        /// <summary>
        /// Fills the map from the skeleton's geometry (not from bone names) and captures the
        /// bind pose. The skeleton root is the wrist; its child with the most bones is the
        /// palm. Of the palm's five chains, the heel points away from the others, the thumb
        /// reaches least far along the finger direction, and the remaining fingers are ordered
        /// by distance from the thumb: index, middle, ring.
        /// </summary>
        [ContextMenu("Auto Map From Skeleton")]
        public void AutoMap()
        {
            if (skin == null) skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null || skin.rootBone == null) return;

            wrist = skin.rootBone;
            palm = null;
            int most = -1;
            foreach (Transform c in wrist)
            {
                int count = c.GetComponentsInChildren<Transform>(true).Length;
                if (count > most) { most = count; palm = c; }
            }
            if (palm == null) return;

            var chains = new List<Transform[]>();
            foreach (Transform c in palm) chains.Add(ChainFrom(c));
            if (chains.Count != 5) { Debug.LogWarning("HandRig: expected 5 chains off the palm, found " + chains.Count, this); return; }

            Vector3 mean = Vector3.zero;
            foreach (Transform[] ch in chains) mean += (Tip(ch) - palm.position).normalized;
            Transform[] heelChain = chains[0];
            float lowest = float.MaxValue;
            foreach (Transform[] ch in chains)
            {
                float d = Vector3.Dot((Tip(ch) - palm.position).normalized, mean);
                if (d < lowest) { lowest = d; heelChain = ch; }
            }
            chains.Remove(heelChain);

            Vector3 fingers = Vector3.zero;
            foreach (Transform[] ch in chains) fingers += (Tip(ch) - palm.position).normalized;
            fingers.Normalize();

            Transform[] thumbChain = chains[0];
            float reach = float.MaxValue;
            foreach (Transform[] ch in chains)
            {
                float r = Vector3.Dot(Tip(ch) - palm.position, fingers);
                if (r < reach) { reach = r; thumbChain = ch; }
            }
            chains.Remove(thumbChain);
            chains.Sort((a, b) => Lateral(a[0], thumbChain[0], fingers).CompareTo(Lateral(b[0], thumbChain[0], fingers)));

            heel = heelChain;
            thumb = thumbChain;
            index = chains[0];
            middle = chains[1];
            ring = chains[2];
            _joints = null;

            CaptureBind();
        }

        public void SetSide(HandSide value) { side = value; }

        // ------------------------------------------------------------------ helpers

        List<Transform> Joints()
        {
            if (_joints != null && Application.isPlaying) return _joints;
            var list = new List<Transform> { wrist, palm };
            AddAll(list, heel);
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

        static int Len(Transform[] a) { return a != null ? a.Length : 0; }

        static bool Has(Transform[] a)
        {
            if (a == null || a.Length == 0) return false;
            foreach (Transform t in a) if (t == null) return false;
            return true;
        }

        static Transform[] ChainFrom(Transform start)
        {
            var list = new List<Transform> { start };
            Transform t = start;
            while (t.childCount > 0) { t = t.GetChild(0); list.Add(t); }
            return list.ToArray();
        }

        static Vector3 Tip(Transform[] chain) { return chain[chain.Length - 1].position; }

        static float Lateral(Transform bone, Transform thumbBase, Vector3 fingers)
        {
            return Vector3.ProjectOnPlane(bone.position - thumbBase.position, fingers).magnitude;
        }
    }
}
