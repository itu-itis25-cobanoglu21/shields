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

        [Tooltip("Head visuals. Hidden for the local player to avoid first person clipping.")]
        [SerializeField] Transform headVisual;

        [Tooltip("Where the single gameplay camera is attached for the local player.")]
        [SerializeField] Transform cameraAnchor;

        [Tooltip("The floating right hand. Its prefab pose is the resting pose; action presentation moves it.")]
        [SerializeField] Transform rightHandAnchor;

        [Header("Body Tint")]
        [Tooltip("Renderers that take the per player placeholder colour.")]
        [SerializeField] Renderer[] tintedRenderers;

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

            // The local player looks out through their own head, so hide it for them only.
            if (headVisual != null) headVisual.gameObject.SetActive(!local);
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
