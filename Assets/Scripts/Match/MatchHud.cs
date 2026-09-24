using UnityEngine;
using Shields.Online;

namespace Shields.Match
{
    /// <summary>Which targeting mode the local player is in, for the HUD only.</summary>
    public enum HudPendingAction
    {
        None = 0,
        Attack = 1,
        ReplaceShield = 2,
        ChargedAttack = 3
    }

    /// <summary>
    /// The screen-space match HUD: turn banner, control hint, and the panel showing the
    /// latest resolved action.
    ///
    /// It draws from a <see cref="PublicMatchSnapshot"/> and a
    /// <see cref="PublicActionResult"/>, which is all it ever needed: the HUD only shows
    /// public information. Because that is exactly what the host sends online and what an
    /// offline match can project locally, one HUD serves both modes rather than each
    /// growing its own copy.
    ///
    /// A plain class, not a MonoBehaviour: whichever component is running the match calls
    /// <see cref="Draw"/> from its own OnGUI.
    /// </summary>
    public sealed class MatchHud
    {
        readonly float _feedbackSeconds;

        GUIStyle _bannerStyle;
        GUIStyle _lineStyle;
        GUIStyle _feedbackHeadlineStyle;
        GUIStyle _feedbackDetailStyle;

        string _feedbackHeadline = string.Empty;
        string _feedbackDetail = string.Empty;
        float _feedbackShownAt = -100f;

        public MatchHud(float feedbackSeconds)
        {
            _feedbackSeconds = feedbackSeconds;
        }

        /// <summary>Shows an arbitrary message, such as "PLAYER 2 STARTS".</summary>
        public void ShowFeedback(string headline, string detail)
        {
            _feedbackHeadline = headline ?? string.Empty;
            _feedbackDetail = detail ?? string.Empty;
            _feedbackShownAt = Time.unscaledTime;
        }

        /// <summary>
        /// Describes a resolved action: actor, action, target, then the public numbers. A
        /// first Charge only ever says that a hidden card was stored.
        /// </summary>
        public void ShowResult(PublicActionResult result, int localSeat)
        {
            if (!result.Accepted) return;

            string actor = PlayerName(result.ActorIndex, localSeat, false);
            string target = PlayerName(result.TargetIndex, localSeat, result.TargetIndex == result.ActorIndex);

            switch (result.ActionType)
            {
                case Shields.Game.TurnActionType.Attack:
                    ShowFeedback(actor + "  ATTACK →  " + target, MatchPresenter.Detail(result));
                    break;

                case Shields.Game.TurnActionType.ReplaceShield:
                    ShowFeedback(actor + "  REPLACE SHIELD →  " + target, "Shield " + MatchPresenter.Detail(result));
                    break;

                default:
                    if (result.IsChargedAttack)
                        ShowFeedback(actor + "  CHARGED ATTACK →  " + target, MatchPresenter.Detail(result));
                    else
                        ShowFeedback(actor + "  CHARGE", "One face-down card stored");
                    break;
            }
        }

        public static string PlayerName(int playerIndex, int localSeat, bool self)
        {
            if (playerIndex < 0) return "NOBODY";
            if (playerIndex == localSeat) return self ? "YOURSELF" : "YOU";

            return "PLAYER " + playerIndex + (self ? " (SELF)" : "");
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>Draws the whole HUD. Call from OnGUI.</summary>
        public void Draw(PublicMatchSnapshot snapshot, int localSeat, HudPendingAction pending,
            int hoveredTarget, string overrideHint = null)
        {
            if (!snapshot.IsValid) return;

            EnsureStyles();

            float scale = Screen.height / 1080f;
            _bannerStyle.fontSize = Mathf.RoundToInt((snapshot.MatchOver ? 64f : 30f) * scale);
            _lineStyle.fontSize = Mathf.RoundToInt(20f * scale);
            _feedbackHeadlineStyle.fontSize = Mathf.RoundToInt(38f * scale);
            _feedbackDetailStyle.fontSize = Mathf.RoundToInt(30f * scale);

            float top = snapshot.MatchOver ? Screen.height * 0.35f : 40f * scale;
            Rect banner = new Rect(0f, top, Screen.width, 80f * scale);
            Rect line = new Rect(0f, banner.yMax, Screen.width, 30f * scale);

            ShadowLabel(banner, BannerText(snapshot, localSeat, pending), _bannerStyle, BannerColor(snapshot, localSeat));
            ShadowLabel(line, overrideHint ?? HintText(snapshot, localSeat, pending, hoveredTarget), _lineStyle, Color.white);

            DrawFeedback(scale);
        }

        void EnsureStyles()
        {
            if (_bannerStyle != null) return;

            _bannerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _lineStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _feedbackHeadlineStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _feedbackDetailStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }

        static string BannerText(PublicMatchSnapshot snapshot, int localSeat, HudPendingAction pending)
        {
            if (snapshot.MatchOver)
                return snapshot.WinnerIndex == localSeat ? "YOU WIN" : "PLAYER " + snapshot.WinnerIndex + " WINS";

            if (snapshot.CurrentPlayerIndex != localSeat)
                return "PLAYER " + snapshot.CurrentPlayerIndex + "'S TURN";

            switch (pending)
            {
                case HudPendingAction.Attack: return "ATTACK — Choose Target";
                case HudPendingAction.ReplaceShield: return "REPLACE SHIELD — Choose Target";
                case HudPendingAction.ChargedAttack: return "CHARGED ATTACK — Choose Target";
                default: return "YOUR TURN";
            }
        }

        static Color BannerColor(PublicMatchSnapshot snapshot, int localSeat)
        {
            if (snapshot.MatchOver)
                return snapshot.WinnerIndex == localSeat ? new Color(0.4f, 1f, 0.5f) : Color.white;

            return snapshot.CurrentPlayerIndex == localSeat ? new Color(1f, 0.85f, 0.2f) : Color.white;
        }

        static string HintText(PublicMatchSnapshot snapshot, int localSeat, HudPendingAction pending, int hoveredTarget)
        {
            if (snapshot.MatchOver) return string.Empty;

            PublicPlayerState local = snapshot.GetPlayer(localSeat);

            if (localSeat < 0) return "Watching the table.";
            if (!local.IsAlive) return "You are eliminated. Watching the table.";
            if (snapshot.CurrentPlayerIndex != localSeat) return string.Empty;

            if (pending == HudPendingAction.None)
                return "[1] Attack    [2] Replace Shield    [3] " + (local.HasCharge ? "Charged Attack" : "Charge");

            string target = hoveredTarget < 0
                ? (pending == HudPendingAction.ReplaceShield
                    ? "Aim at a player or a Shield card (your own for yourself)"
                    : "Aim at another player or their Shield card")
                : "Click to confirm: " + (hoveredTarget == localSeat ? "YOURSELF" : "PLAYER " + hoveredTarget);

            return target + "    ([1]/[2]/[3] switch action)";
        }

        void DrawFeedback(float scale)
        {
            float age = Time.unscaledTime - _feedbackShownAt;
            if (age >= _feedbackSeconds || string.IsNullOrEmpty(_feedbackHeadline)) return;

            // Fades out over the last half second instead of popping away.
            float alpha = Mathf.Clamp01((_feedbackSeconds - age) / 0.5f);

            float width = Mathf.Min(Screen.width - 40f * scale, 1100f * scale);
            Rect panel = new Rect((Screen.width - width) * 0.5f, 175f * scale, width, 112f * scale);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            Rect headline = new Rect(panel.x, panel.y + 10f * scale, panel.width, 50f * scale);
            Rect detail = new Rect(panel.x, headline.yMax, panel.width, 42f * scale);

            ShadowLabel(headline, _feedbackHeadline, _feedbackHeadlineStyle, new Color(1f, 1f, 1f, alpha));
            ShadowLabel(detail, _feedbackDetail, _feedbackDetailStyle, new Color(1f, 0.9f, 0.55f, alpha));
        }

        public static void ShadowLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f * color.a);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }
    }
}
