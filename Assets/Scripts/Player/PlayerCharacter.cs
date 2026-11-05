using UnityEngine;

namespace Shields.Players
{
    /// <summary>
    /// Runtime identity and visual rig of one seated player. The same prefab is used
    /// for the local human and for every bot, so nothing here assumes a single human.
    ///
    /// Identity (who this player is), control (human or bot) and representation
    /// (the body and head visuals) are kept separate on purpose, so a networking layer
    /// can later own identity without touching the visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerCharacter : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] int playerIndex = -1;
        [SerializeField] bool isLocalPlayer;
        [SerializeField] bool isBot;

        [Header("Rig")]
        [Tooltip("Body visuals. Replace the placeholder primitives with real character art here.")]
        [SerializeField] Transform body;

        [Tooltip("Rotated by look input. Everything head mounted is parented under this.")]
        [SerializeField] Transform headPivot;

        [Tooltip("Head visuals. Always present; for the local player they go on the LocalHead layer, which only this machine's gameplay camera skips.")]
        [SerializeField] Transform headVisual;

        [Tooltip("Where the single gameplay camera is attached for the local player.")]
        [SerializeField] Transform cameraAnchor;

        [Tooltip("The floating right hand. Its prefab pose is the resting pose; action presentation moves it.")]
        [SerializeField] Transform rightHandAnchor;

        [Tooltip("The floating left hand. Its prefab pose is the resting pose; targeted actions move it to the Draw Pile.")]
        [SerializeField] Transform leftHandAnchor;

        [Header("Weapon (presentation only)")]
        [Tooltip("Where the weapon rests while holstered, at the right hip.")]
        [SerializeField] Transform weaponHolsterAnchor;

        [Tooltip("Under the right hand: where the weapon's grip sits while it is held.")]
        [SerializeField] Transform weaponGripAnchor;

        [Tooltip("The one weapon visual of this player. Its root is the grip, its +Z the barrel.")]
        [SerializeField] Transform weapon;

        [Header("Hit Reaction (presentation only)")]
        [Tooltip("Fixed base at the bottom of the body that the upper body tilts around when hit. Never moved itself.")]
        [SerializeField] Transform hitReactionPivot;

        [Header("Body Tint")]
        [Tooltip("Renderers that take the per player placeholder colour.")]
        [SerializeField] Renderer[] tintedRenderers;

        /// <summary>
        /// The layer the local player's own head is drawn on. The first-person gameplay
        /// camera leaves this one layer out, so the camera never looks out through the
        /// inside of its own head, while the head stays a real, visible part of the
        /// character everywhere else (Scene view, other cameras, other machines).
        /// </summary>
        public const string LocalHeadLayerName = "LocalHead";

        /// <summary>The LocalHead layer index, or -1 when the project does not define it.</summary>
        public static int LocalHeadLayer { get { return LayerMask.NameToLayer(LocalHeadLayerName); } }

        // The head's own layer from the prefab, restored when this seat is not local.
        int _headLayer = -1;

        public int PlayerIndex { get { return playerIndex; } }
        public bool IsLocalPlayer { get { return isLocalPlayer; } }
        public bool IsBot { get { return isBot; } }

        public Transform Body { get { return body; } }
        public Transform HeadPivot { get { return headPivot; } }
        public Transform HeadVisual { get { return headVisual; } }
        public Transform CameraAnchor { get { return cameraAnchor; } }

        public Transform RightHandAnchor
        {
            get
            {
                if (rightHandAnchor == null) rightHandAnchor = transform.Find("RightShoulderAnchor/RightHandAnchor");
                return rightHandAnchor;
            }
        }

        public Transform LeftHandAnchor
        {
            get
            {
                if (leftHandAnchor == null) leftHandAnchor = transform.Find("LeftShoulderAnchor/LeftHandAnchor");
                return leftHandAnchor;
            }
        }

        /// <summary>Finger rig of the left hand visual (inside LeftHandAnchor/Hand_L), or null.</summary>
        public HandRig LeftHand
        {
            get
            {
                if (_leftHand == null && LeftHandAnchor != null) _leftHand = LeftHandAnchor.GetComponentInChildren<HandRig>(true);
                return _leftHand;
            }
        }

        /// <summary>Finger rig of the right hand visual (inside RightHandAnchor/Hand_R), or null.</summary>
        public HandRig RightHand
        {
            get
            {
                if (_rightHand == null && RightHandAnchor != null) _rightHand = RightHandAnchor.GetComponentInChildren<HandRig>(true);
                return _rightHand;
            }
        }

        HandRig _leftHand;
        HandRig _rightHand;

        public Transform WeaponHolsterAnchor { get { return weaponHolsterAnchor; } }
        public Transform WeaponGripAnchor { get { return weaponGripAnchor; } }
        public Transform Weapon { get { return weapon; } }

        public Transform HitReactionPivot
        {
            get
            {
                if (hitReactionPivot == null) hitReactionPivot = transform.Find("HitReactionPivot");
                return hitReactionPivot;
            }
        }

        /// <summary>
        /// A stable upper-body point to aim at: between the body's centre and the head,
        /// above the tabletop, never the chair. Presentation only.
        /// </summary>
        public Vector3 UpperBodyAimPoint(float towardHead)
        {
            Transform head = headPivot != null ? headPivot : transform;

            Renderer bodyRenderer = body != null ? body.GetComponentInChildren<Renderer>() : null;
            Vector3 bodyCenter = bodyRenderer != null ? bodyRenderer.bounds.center : transform.position + Vector3.up * 0.5f;

            return Vector3.Lerp(bodyCenter, head.position, towardHead);
        }

        /// <summary>
        /// Assigns the identity this player will keep for the match. Called once by the
        /// match bootstrap. A networking layer would call this from spawn data instead.
        /// </summary>
        public void AssignIdentity(int index, bool local, bool bot)
        {
            playerIndex = index;
            isLocalPlayer = local;
            isBot = bot;

            name = string.Format("Player_{0:00}", index);

            ApplyHeadVisibility(local);
        }

        /// <summary>
        /// Every player keeps their head. The local player's head goes on the LocalHead
        /// layer so only their own first-person camera skips it; anyone else's head is on
        /// its normal layer. Called on every identity change, so a seat that stops being
        /// local (rematch, new seating) gets its normal head back.
        /// </summary>
        void ApplyHeadVisibility(bool local)
        {
            if (headVisual == null) return;
            if (_headLayer < 0) _headLayer = headVisual.gameObject.layer;

            int localLayer = LocalHeadLayer;
            if (localLayer < 0)
            {
                // Without the layer the camera cannot skip the head: hide it on this machine instead.
                headVisual.gameObject.SetActive(!local);
                return;
            }

            headVisual.gameObject.SetActive(true);
            SetLayerRecursively(headVisual, local ? localLayer : _headLayer);
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }

        /// <summary>Applies the placeholder colour that tells seats apart while testing.</summary>
        public void ApplyBodyMaterial(Material material)
        {
            if (material == null || tintedRenderers == null) return;

            for (int i = 0; i < tintedRenderers.Length; i++)
                if (tintedRenderers[i] != null) tintedRenderers[i].sharedMaterial = material;
        }

        /// <summary>Editor convenience so the prefab can be rewired without hand dragging.</summary>
        public void BindRig(Transform bodyTransform, Transform pivot, Transform head, Transform anchor, Renderer[] tinted)
        {
            body = bodyTransform;
            headPivot = pivot;
            headVisual = head;
            cameraAnchor = anchor;
            tintedRenderers = tinted;
        }
    }
}
