using UnityEngine;
using Shields.Table;

namespace Shields.Match
{
    /// <summary>
    /// Temporary player count picker for the offline prototype. Deliberately built with
    /// IMGUI so it adds no canvas, no event system and no prefabs to the scene, and so
    /// it is obvious that it is scaffolding to be replaced by a real lobby later.
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
            const float width = 380f;
            const float height = 150f;

            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.Box(panel, "Prototype Match Setup");
            GUILayout.BeginArea(new Rect(panel.x + 14f, panel.y + 30f, panel.width - 28f, panel.height - 44f));

            GUILayout.Label("How many players at the table?");

            GUILayout.BeginHorizontal();
            for (int count = SeatLayout.MinPlayers; count <= SeatLayout.MaxPlayers; count++)
            {
                bool selected = bootstrap.TargetPlayerCount == count;
                string label = selected ? "[" + count + "]" : count.ToString();

                if (GUILayout.Button(label, GUILayout.Height(28f)))
                    bootstrap.TargetPlayerCount = count;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label(string.Format("Seat {0} is you. The other {1} are bot placeholders.",
                bootstrap.LocalPlayerIndex, bootstrap.TargetPlayerCount - 1));

            if (GUILayout.Button("Start Match", GUILayout.Height(30f)))
                bootstrap.StartMatch(bootstrap.TargetPlayerCount);

            GUILayout.EndArea();
        }

        void DrawInMatchHint()
        {
            Rect hint = new Rect(12f, 12f, 360f, 92f);
            GUI.Box(hint, GUIContent.none);

            GUILayout.BeginArea(new Rect(hint.x + 10f, hint.y + 8f, hint.width - 20f, hint.height - 16f));
            GUILayout.Label(string.Format("{0} players, seat {1} is you.",
                bootstrap.TargetPlayerCount, bootstrap.LocalPlayerIndex));
            GUILayout.Label("Move the mouse to look. Esc frees the cursor.");

            if (GUILayout.Button("Change player count", GUILayout.Height(24f)))
                bootstrap.EndMatch();

            GUILayout.EndArea();
        }
    }
}
