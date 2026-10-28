using System.Collections.Generic;
using UnityEngine;
using Shields.Players;

namespace Shields.Table
{
    /// <summary>
    /// Places players and their card areas evenly around the round table for any
    /// supported player count, and resizes only the visible tabletop to suit.
    ///
    /// This owns placement, not identity. It spawns the shared PlayerCharacter prefab
    /// and hands the instances back, leaving the match bootstrap to decide which seat is
    /// local and which are bots. The static Environment and Table are never touched
    /// beyond scaling the tabletop meshes.
    /// </summary>
    [DisallowMultipleComponent]
    public class TableLayoutController : MonoBehaviour
    {
        [Header("Static Table References")]
        [Tooltip("The persistent Table object. Its position is the center of the layout.")]
        [SerializeField] Transform tableRoot;

        [Tooltip("Sits on the felt. Supplies the height card areas are placed at.")]
        [SerializeField] Transform tableSurface;

        [Tooltip("Where the remaining deck lives. Also the point players' heads rest on.")]
        [SerializeField] Transform centerDeckAnchor;

        [Header("Tabletop Meshes To Resize")]
        [Tooltip("Only these meshes are scaled. Gameplay anchors are siblings, so they never inherit it.")]
        [SerializeField] Transform[] tabletopMeshes;

        [Header("Runtime Containers")]
        [SerializeField] Transform runtimePlayersRoot;
        [SerializeField] Transform runtimeTableAreasRoot;

        [Header("Prefabs")]
        [Tooltip("One prefab for humans and bots alike.")]
        [SerializeField] PlayerCharacter playerPrefab;

        [SerializeField] PlayerTableArea tableAreaPrefab;

        [Header("Layout")]
        [SerializeField] TableLayoutSettings layout = new TableLayoutSettings();

        [Tooltip("Scale the tabletop meshes with the player count.")]
        [SerializeField] bool resizeTabletop = true;

        [Header("Placeholders")]
        [Tooltip("Placeholder body colours, indexed by player. Purely to tell seats apart while testing.")]
        [SerializeField] Material[] playerMaterials;

        readonly List<PlayerCharacter> _players = new List<PlayerCharacter>();
        readonly List<PlayerTableArea> _tableAreas = new List<PlayerTableArea>();

        TableLayoutMetrics _metrics;
        Vector3[] _tabletopBaseScales;

        public TableLayoutSettings Layout { get { return layout; } }
        public TableLayoutMetrics Metrics { get { return _metrics; } }
        public IReadOnlyList<PlayerCharacter> Players { get { return _players; } }
        public IReadOnlyList<PlayerTableArea> TableAreas { get { return _tableAreas; } }

        /// <summary>The point seated players naturally rest their view on.</summary>
        public Vector3 DeckAimPoint
        {
            get { return centerDeckAnchor != null ? centerDeckAnchor.position : TableCenter; }
        }

        /// <summary>Centre of the table on the floor plane; seats are placed around it.</summary>
        public Vector3 TableCenter { get { return tableRoot != null ? tableRoot.position : transform.position; } }

        float CardAreaHeight
        {
            get { return tableSurface != null ? tableSurface.position.y : TableCenter.y; }
        }

        void Awake()
        {
            CacheTabletopBaseScales();
        }

        /// <summary>
        /// Spawns one player and one card area per seat, evenly spaced by 360 / count.
        /// Any previous layout is cleared first, so this is safe to call repeatedly.
        /// </summary>
        public void BuildLayout(int playerCount)
        {
            ClearLayout();
            CacheTabletopBaseScales();

            _metrics = SeatLayout.Resolve(playerCount, layout);

            if (resizeTabletop) ResizeTabletop(_metrics.TableRadius);

            for (int i = 0; i < _metrics.PlayerCount; i++)
            {
                SpawnPlayer(i);
                SpawnTableArea(i);
            }
        }

        /// <summary>Destroys the runtime players and card areas. Static scene content is untouched.</summary>
        public void ClearLayout()
        {
            _players.Clear();
            _tableAreas.Clear();

            ClearChildren(runtimePlayersRoot);
            ClearChildren(runtimeTableAreasRoot);
        }

        public PlayerTableArea GetAreaForPlayer(int playerIndex)
        {
            for (int i = 0; i < _tableAreas.Count; i++)
                if (_tableAreas[i].PlayerIndex == playerIndex) return _tableAreas[i];

            return null;
        }

        public PlayerCharacter GetPlayer(int playerIndex)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].PlayerIndex == playerIndex) return _players[i];

            return null;
        }

        public string DescribeLayout()
        {
            TableLayoutMetrics m = SeatLayout.Resolve(_metrics.PlayerCount > 0 ? _metrics.PlayerCount : SeatLayout.MinPlayers, layout);
            return string.Format(
                "{0} seats | step {1:0.0} deg | table diameter {2:0.00} m | seat ring radius {3:0.00} m | neighbour gap {4:0.00} m | across table {5:0.00} m",
                m.PlayerCount, m.SeatAngleStep, m.TableRadius * 2f, m.SeatRadius,
                SeatLayout.NeighbourSpacing(m), m.SeatRadius * 2f);
        }

        // ------------------------------------------------------------------ spawning

        void SpawnPlayer(int index)
        {
            if (playerPrefab == null || runtimePlayersRoot == null) return;

            PlayerCharacter player = Instantiate(playerPrefab, runtimePlayersRoot);

            Vector3 center = TableCenter;
            Vector3 offset = SeatLayout.SeatOffset(index, _metrics);
            player.transform.SetPositionAndRotation(
                new Vector3(center.x + offset.x, center.y, center.z + offset.z),
                SeatLayout.SeatRotation(index, _metrics));

            // Identity is assigned by the bootstrap. Placement only sets the index.
            player.AssignIdentity(index, false, false);
            player.ApplyBodyMaterial(MaterialForPlayer(index));

            _players.Add(player);
        }

        void SpawnTableArea(int index)
        {
            if (tableAreaPrefab == null || runtimeTableAreasRoot == null) return;

            PlayerTableArea area = Instantiate(tableAreaPrefab, runtimeTableAreasRoot);

            Vector3 center = TableCenter;
            Vector3 offset = SeatLayout.CardAreaOffset(index, _metrics);
            area.transform.SetPositionAndRotation(
                new Vector3(center.x + offset.x, CardAreaHeight, center.z + offset.z),
                SeatLayout.SeatRotation(index, _metrics));

            // Scaling the area root scales the card views with their slot spacing, so
            // the three card arrangement keeps its shape at any size.
            area.transform.localScale = Vector3.one * Mathf.Max(0.1f, layout.cardAreaScale);

            area.AssignPlayer(index);

            // Cards are placed by the match setup once the deal happens.
            area.ClearCards();

            _tableAreas.Add(area);
        }

        Material MaterialForPlayer(int index)
        {
            if (playerMaterials == null || playerMaterials.Length == 0) return null;

            return playerMaterials[index % playerMaterials.Length];
        }

        // ------------------------------------------------------------------ tabletop

        void CacheTabletopBaseScales()
        {
            if (tabletopMeshes == null) return;
            if (_tabletopBaseScales != null && _tabletopBaseScales.Length == tabletopMeshes.Length) return;

            _tabletopBaseScales = new Vector3[tabletopMeshes.Length];
            for (int i = 0; i < tabletopMeshes.Length; i++)
                _tabletopBaseScales[i] = tabletopMeshes[i] != null ? tabletopMeshes[i].localScale : Vector3.one;
        }

        /// <summary>
        /// Scales only the tabletop meshes, and only on the horizontal axes, so the
        /// surface height and every gameplay anchor stay exactly where they were.
        /// </summary>
        void ResizeTabletop(float tableRadius)
        {
            if (tabletopMeshes == null) return;

            float referenceRadius = Mathf.Max(0.01f, layout.minTableRadius);

            for (int i = 0; i < tabletopMeshes.Length; i++)
            {
                Transform mesh = tabletopMeshes[i];
                if (mesh == null) continue;

                Vector3 baseScale = _tabletopBaseScales[i];
                float factor = tableRadius / referenceRadius;
                mesh.localScale = new Vector3(baseScale.x * factor, baseScale.y, baseScale.z * factor);
            }
        }

        // ------------------------------------------------------------------ misc

        static void ClearChildren(Transform root)
        {
            if (root == null) return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;

                if (Application.isPlaying)
                {
                    // Destroy is deferred to the end of the frame, so detach first.
                    // Without this, rebuilding twice in one frame would stack two layouts.
                    child.transform.SetParent(null, false);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}
