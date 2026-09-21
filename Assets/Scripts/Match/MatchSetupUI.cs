using UnityEngine;
using Shields.Cards;
using Shields.Game;

namespace Shields.Match
{
    /// <summary>
    /// Temporary match configuration panel for the offline prototype: player count and
    /// deck count. Deliberately built with IMGUI so it adds no canvas, no event system
    /// and no prefabs to the scene, and so it is obvious that it is scaffolding to be
    /// replaced by a real lobby later.
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
            if (bootstrap == null) return;

            if (!bootstrap.MatchRunning) DrawSetupPanel();
            else if (showInMatchHint) DrawInMatchHint();
        }

        void DrawSetupPanel()
        {
            const float width = 400f;
            const float height = 216f;

            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.Box(panel, "Prototype Match Setup");
            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 30f, panel.width - 28f, panel.height - 44f));

            GUILayout.Label("Players at the table");
            GUILayout.BeginHorizontal();
            for (int count = MatchConfig.MinPlayers; count <= MatchConfig.MaxPlayers; count++)
            {
                bool selected = bootstrap.TargetPlayerCount == count;
                if (GUILayout.Button(selected ? "[" + count + "]" : count.ToString(), GUILayout.Height(26f)))
                    bootstrap.TargetPlayerCount = count;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Decks in play");
            GUILayout.BeginHorizontal();
            for (int decks = CardDeck.MinDeckCount; decks <= CardDeck.MaxDeckCount; decks++)
            {
                bool selected = bootstrap.DeckCount == decks;
                string label = string.Format("{0}{1} deck{2} ({3} cards){4}",
                    selected ? "[" : "", decks, decks == 1 ? "" : "s", decks * 52, selected ? "]" : "");

                if (GUILayout.Button(label, GUILayout.Height(26f)))
                    bootstrap.DeckCount = decks;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label(string.Format("Seat {0} is you. The other {1} are bot placeholders.",
                bootstrap.LocalPlayerIndex, bootstrap.TargetPlayerCount - 1));

            if (GUILayout.Button("Deal and Start", GUILayout.Height(30f)))
                bootstrap.StartMatch(bootstrap.TargetPlayerCount, bootstrap.DeckCount);

            GUILayout.EndArea();
        }

        void DrawInMatchHint()
        {
            Rect hint = new Rect(12f, 12f, 380f, 112f);
            GUI.Box(hint, GUIContent.none);

            GUILayout.BeginArea(new Rect(hint.x + 10f, hint.y + 8f, hint.width - 20f, hint.height - 16f));

            GUILayout.Label(string.Format("{0} players, {1} deck(s), seat {2} is you.",
                bootstrap.TargetPlayerCount, bootstrap.DeckCount, bootstrap.LocalPlayerIndex));

            if (bootstrap.AwaitingLocalShieldChoice)
            {
                GUILayout.Label("Click one of your three cards to make it your Shield.");
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

            if (GUILayout.Button("Change setup", GUILayout.Height(24f)))
                bootstrap.EndMatch();

            GUILayout.EndArea();
        }
    }
}
