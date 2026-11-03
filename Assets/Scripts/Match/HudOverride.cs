using System;
using Shields.Online;
using UnityEngine;

namespace Shields.Match
{
    /// <summary>
    /// Lets a replacement screen HUD take over from the IMGUI prototype HUD.
    ///
    /// While <see cref="Active"/> is true the prototype banner, hint, feedback panel,
    /// setup hint, beer prompts, rematch prompt and diagnostics line stop drawing. The
    /// match keeps feeding the same public data it always drew from: the latest frame
    /// (<see cref="Latest"/>) and every feedback message (<see cref="Feedback"/>).
    /// Off by default, so nothing changes unless a replacement HUD switches it on.
    /// Presentation only.
    /// </summary>
    public static class HudOverride
    {
        /// <summary>What the match HUD was asked to draw most recently.</summary>
        public struct Frame
        {
            public PublicMatchSnapshot Snapshot;
            public int LocalSeat;
            public HudPendingAction Pending;
            public int HoveredTarget;
            public int FrameCount;
        }

        public static bool Active;

        /// <summary>
        /// While <see cref="Active"/>, the replacement HUD also draws the open beer order
        /// panel and its feedback line (through BeerOrderUI's public API).
        /// </summary>
        public static bool BeerPanel;

        public static Frame Latest;

        /// <summary>Raised for every headline / detail the match HUD shows (starts, resolved actions).</summary>
        public static event Action<string, string> Feedback;

        /// <summary>Raised when a resolved action is revealed on the table (same moment the HUD line appears), with the local seat.</summary>
        public static event Action<PublicActionResult, int> Result;

        /// <summary>
        /// Raised when the table starts presenting an accepted action (before its card is
        /// drawn and revealed), with the local seat. Only actor, target and action type are
        /// public at this moment: a listener must not show anything else (cards, damage)
        /// before <see cref="Result"/> arrives.
        /// </summary>
        public static event Action<PublicActionResult, int> ActionStarted;

        /// <summary>True while a match HUD frame was recorded this frame or the last.</summary>
        public static bool HasCurrentFrame { get { return Latest.Snapshot.IsValid && Time.frameCount - Latest.FrameCount <= 1; } }

        internal static void Record(PublicMatchSnapshot snapshot, int localSeat, HudPendingAction pending, int hoveredTarget)
        {
            Latest = new Frame { Snapshot = snapshot, LocalSeat = localSeat, Pending = pending, HoveredTarget = hoveredTarget, FrameCount = Time.frameCount };
        }

        internal static void RaiseFeedback(string headline, string detail)
        {
            Feedback?.Invoke(headline, detail);
        }

        internal static void RaiseResult(PublicActionResult result, int localSeat)
        {
            Result?.Invoke(result, localSeat);
        }

        internal static void RaiseActionStarted(PublicActionResult result, int localSeat)
        {
            ActionStarted?.Invoke(result, localSeat);
        }

        // Statics survive Play Mode restarts when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Active = false;
            BeerPanel = false;
            Latest = default;
            Feedback = null;
            Result = null;
            ActionStarted = null;
        }
    }
}
