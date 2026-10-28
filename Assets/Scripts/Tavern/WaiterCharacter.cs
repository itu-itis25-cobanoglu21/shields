using System.Collections.Generic;
using UnityEngine;

namespace Shields.Tavern
{
    /// <summary>
    /// The waiter's visual rig. Presentation only: no input, camera, seat, network or
    /// gameplay component, and no colliders, so it can never be a crosshair target.
    ///
    /// Three parts move differently on purpose:
    /// - the root travels along the route and turns smoothly;
    /// - <see cref="BodyBobRoot"/> (body, head, the delivery hand's rest) gets a very
    ///   small procedural walking bob and sway;
    /// - <see cref="TrayHand"/> is a sibling of the bob root, not a child, so the tray is
    ///   carried at a steady height while the body bobs.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaiterCharacter : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] Transform bodyBobRoot;

        [Tooltip("Right hand. Rests at DeliveryHandRest; the presenter drives it while serving.")]
        [SerializeField] Transform deliveryHand;

        [Tooltip("Under BodyBobRoot, so the resting delivery hand bobs with the body.")]
        [SerializeField] Transform deliveryHandRest;

        [Tooltip("Left hand. Not under the bob root: the tray stays level.")]
        [SerializeField] Transform trayHand;

        [Tooltip("The tray sits here; mugs go under MugSlots.")]
        [SerializeField] Transform trayAnchor;

        [SerializeField] Transform mugSlots;

        [Header("Walking Bob")]
        [Tooltip("Vertical body bob in meters at full walking speed.")]
        [Range(0f, 0.06f)]
        [SerializeField] float bobAmplitude = 0.025f;

        [Tooltip("Bob cycles per second.")]
        [Range(0.5f, 4f)]
        [SerializeField] float bobFrequency = 1.8f;

        [Tooltip("Side to side body roll in degrees. Keep it barely visible.")]
        [Range(0f, 4f)]
        [SerializeField] float swayDegrees = 0.8f;

        [Tooltip("How fast the bob fades in and out when walking starts or stops.")]
        [SerializeField] float bobBlendSpeed = 6f;

        readonly List<Transform> _mugs = new List<Transform>();
        Vector3 _bobBaseLocal;
        Quaternion _bobBaseRotation;
        float _phase;
        float _weight;

        public Transform BodyBobRoot { get { return bodyBobRoot; } }
        public Transform DeliveryHand { get { return deliveryHand; } }
        public Transform DeliveryHandRest { get { return deliveryHandRest; } }
        public Transform TrayHand { get { return trayHand; } }
        public Transform TrayAnchor { get { return trayAnchor; } }

        /// <summary>The mugs still on the tray, in tray slot order. Null entries were handed over.</summary>
        public IReadOnlyList<Transform> Mugs { get { return _mugs; } }

        /// <summary>0 when standing, 1 when walking at full speed. Set by the presenter.</summary>
        public float WalkWeight { get; set; }

        /// <summary>While true the presenter owns the delivery hand's pose.</summary>
        public bool DeliveryHandBusy { get; set; }

        /// <summary>Current body bob offset in meters, for validation.</summary>
        public float CurrentBobOffset { get; private set; }

        void Awake()
        {
            if (bodyBobRoot != null)
            {
                _bobBaseLocal = bodyBobRoot.localPosition;
                _bobBaseRotation = bodyBobRoot.localRotation;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _weight = Mathf.MoveTowards(_weight, Mathf.Clamp01(WalkWeight), bobBlendSpeed * dt);
            // |sin| repeats every PI, so this gives bobFrequency lifts per second; the sway
            // (plain sin) completes one side to side per two steps.
            _phase += dt * bobFrequency * Mathf.PI * Mathf.Max(_weight, 0.0001f);

            if (bodyBobRoot != null)
            {
                // Two lifts per cycle like footfalls: |sin| rises from zero, never sinks below rest.
                CurrentBobOffset = bobAmplitude * _weight * Mathf.Abs(Mathf.Sin(_phase));
                bodyBobRoot.localPosition = _bobBaseLocal + Vector3.up * CurrentBobOffset;
                bodyBobRoot.localRotation = _bobBaseRotation *
                    Quaternion.Euler(0f, 0f, swayDegrees * _weight * Mathf.Sin(_phase));
            }

            if (!DeliveryHandBusy && deliveryHand != null && deliveryHandRest != null)
                deliveryHand.SetPositionAndRotation(deliveryHandRest.position, deliveryHandRest.rotation);
        }

        /// <summary>
        /// Puts exactly <paramref name="count"/> real mugs on the tray, one per recipient.
        /// Physics and colliders are stripped so a mug can never be a crosshair target or fall.
        /// </summary>
        public void LoadMugs(GameObject mugPrefab, int count)
        {
            ClearMugs();
            if (mugPrefab == null || mugSlots == null) return;

            count = Mathf.Clamp(count, 0, TrayMugLayout.MaxMugs);
            for (int i = 0; i < count; i++)
            {
                Transform slot = new GameObject("MugSlot_" + i).transform;
                slot.SetParent(mugSlots, false);

                Vector3 position;
                Quaternion rotation;
                TrayMugLayout.Pose(i, count, out position, out rotation);
                slot.localPosition = position;
                slot.localRotation = rotation;

                GameObject mug = Instantiate(mugPrefab, slot, false);
                mug.transform.localPosition = Vector3.zero;
                mug.transform.localRotation = Quaternion.identity;
                StripPhysics(mug);

                _mugs.Add(mug.transform);
            }
        }

        /// <summary>Takes a mug off the tray's books once the hand has it. The object itself moves on.</summary>
        public Transform ReleaseMug(int index)
        {
            if (index < 0 || index >= _mugs.Count) return null;

            Transform mug = _mugs[index];
            _mugs[index] = null;
            return mug;
        }

        public int MugsOnTray
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _mugs.Count; i++) if (_mugs[i] != null) count++;
                return count;
            }
        }

        void ClearMugs()
        {
            _mugs.Clear();
            if (mugSlots == null) return;

            for (int i = mugSlots.childCount - 1; i >= 0; i--) Destroy(mugSlots.GetChild(i).gameObject);
        }

        public static void StripPhysics(GameObject root)
        {
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>()) Destroy(body);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>()) Destroy(collider);
        }

        /// <summary>Editor wiring used when the prefab is built from code.</summary>
        public void BindRig(Transform bob, Transform hand, Transform handRest, Transform tray, Transform anchor, Transform slots)
        {
            bodyBobRoot = bob;
            deliveryHand = hand;
            deliveryHandRest = handRest;
            trayHand = tray;
            trayAnchor = anchor;
            mugSlots = slots;
        }
    }
}
