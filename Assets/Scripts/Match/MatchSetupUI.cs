using UnityEngine;
using Shields.Game;

namespace Shields.Match
{
    /// <summary>
    /// The small in-match hint for the offline table: who you are, what to do next, and
    /// your Health / Shield. The table itself is now chosen on the main menu (players and
    /// decks), so there is no setup panel here any more. IMGUI, like the rest of the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MatchBootstrap))]
    public class MatchSetupUI : MonoBehaviour
    {
        [SerializeField] MatchBootstrap bootstrap;

        [Tooltip("Hide the in match hint overlay.")]
        [SerializeField] bool showInMatchHint = true;

        void Awake()
        {
            if (bootstrap == null) bootstrap = GetComponent<MatchBootstrap>();
        }

        void OnGUI()
        {
            if (bootstrap == null || !bootstrap.MatchRunning || !showInMatchHint) return;

            DrawInMatchHint();
        }

        void DrawInMatchHint()
        {
            Rect hint = new Rect(12f, 12f, 380f, 84f);
            GUI.Box(hint, GUIContent.none);

            GUILayout.BeginArea(new Rect(hint.x + 10f, hint.y + 8f, hint.width - 20f, hint.height - 16f));

            GUILayout.Label(string.Format("{0} players, {1} deck(s), seat {2} is you.",
                bootstrap.TargetPlayerCount, bootstrap.DeckCount, bootstrap.LocalPlayerIndex));

            if (bootstrap.AwaitingLocalShieldChoice)
            {
                GUILayout.Label("Look at one of your three cards and click to make it your Shield.");
            }
            else if (bootstrap.Match != null)
            {
                ShieldsMatch match = bootstrap.Match;
                MatchPlayerState local = match.Players[bootstrap.LocalPlayerIndex];
                GUILayout.Label(local.IsAlive
                    ? string.Format("Your Health {0}, Shield {1}{2}. Deck {3} left.",
                        local.Health, local.ShieldValue, local.HasCharge ? ", Charge stored" : "", match.Deck.DrawCount)
                    : string.Format("You are eliminated. Deck {0} left.", match.Deck.DrawCount));
            }
            else if (bootstrap.InitialSetupComplete)
            {
                MatchSetup setup = bootstrap.Setup;
                GUILayout.Label(string.Format("Initial setup complete. Your Health {0}, Shield {1}. Deck {2} left.",
                    setup.LocalPlayer.StartingHealth, setup.LocalPlayer.ShieldStrength, setup.Deck.DrawCount));
            }
            else
            {
                GUILayout.Label("Move the mouse to look. Esc frees the cursor.");
            }

            GUILayout.EndArea();
        }
    }
}
