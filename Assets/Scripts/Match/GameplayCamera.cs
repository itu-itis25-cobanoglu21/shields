using UnityEngine;

namespace Shields.Match
{
    /// <summary>
    /// The single gameplay camera in the scene. It is parked at an overview pose until a
    /// match starts, then reparented onto the local player's camera anchor.
    ///
    /// Nothing here knows which player is local. The match bootstrap decides that and
    /// passes the anchor in, so the camera system stays free of any player 0 assumption.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class GameplayCamera : MonoBehaviour
    {
        Transform _parkedParent;
        Vector3 _parkedPosition;
        Quaternion _parkedRotation;
        bool _parkedPoseStored;

        public Transform AttachedAnchor { get; private set; }

        void Awake()
        {
            StoreParkedPose();
        }

        void StoreParkedPose()
        {
            if (_parkedPoseStored) return;

            _parkedParent = transform.parent;
            _parkedPosition = transform.position;
            _parkedRotation = transform.rotation;
            _parkedPoseStored = true;
        }

        /// <summary>Reparents the camera onto a player's camera anchor with no offset.</summary>
        public void AttachTo(Transform anchor)
        {
            if (anchor == null) return;

            StoreParkedPose();

            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            AttachedAnchor = anchor;
        }

        /// <summary>Returns the camera to its overview pose so no player owns it.</summary>
        public void Detach()
        {
            if (!_parkedPoseStored) return;

            transform.SetParent(_parkedParent, false);
            transform.SetPositionAndRotation(_parkedPosition, _parkedRotation);
            transform.localScale = Vector3.one;

            AttachedAnchor = null;
        }
    }
}
