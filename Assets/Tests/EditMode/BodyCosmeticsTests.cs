using System.Collections.Generic;
using NUnit.Framework;
using Shields.Social;
using UnityEditor;
using UnityEngine;

namespace Shields.Game.Tests
{
    /// <summary>
    /// BODY cosmetics: Bare plus the sixteen exported outfits, the old body colours kept as an
    /// independent choice, safe fallbacks for saved ids, cosmetics following the participant
    /// rather than the seat, and the exported outfit assets fitting the shared body anchor.
    /// </summary>
    public class BodyCosmeticsTests
    {
        const string OutfitFolder = "Assets/Art/Characters/Cosmetics/body_accessories/";
        const string PlayerPrefab = "Assets/Prefabs/PlayerCharacter.prefab";

        static readonly string[] ExportedOutfits =
        {
            "TavernVest", "GamblersLongcoat", "TravellersWrap", "MerchantRobeShell", "GuardChestpiece",
            "NobleDoublet", "BarkeepApron", "OccultCardseerGarment", "PatchworkRogueJerkin",
            "TravelingMinstrelWrap", "BrewersLeatherHarness", "MonksShoulderMantle", "DuelistsShortJacket",
            "RaggedOutcastWrap", "WinterFurCollarOutfit", "TavernChampionOutfit",
        };

        // ------------------------------------------------------------------ catalog

        [Test]
        public void Outfits_AreBarePlusTheSixteenExports()
        {
            Assert.AreEqual(17, BodyCatalog.Outfits.Count);
            Assert.AreEqual(BodyCatalog.BareOutfitId, BodyCatalog.Outfits[0].Id);
            Assert.IsTrue(BodyCatalog.Outfits[0].IsDefault);
            Assert.IsNull(BodyCatalog.Outfits[0].SourceAsset);

            var sources = new List<string>();
            var ids = new HashSet<string>();
            foreach (BodyOutfit o in BodyCatalog.Outfits)
            {
                Assert.IsTrue(ids.Add(o.Id), "duplicate id " + o.Id);
                if (o.Id == BodyCatalog.BareOutfitId) continue;
                Assert.IsFalse(o.IsDefault, o.Id);
                sources.Add(o.SourceAsset);
            }
            CollectionAssert.AreEquivalent(ExportedOutfits, sources);
        }

        [Test]
        public void Colors_KeepEveryOldBodyColorAndId()
        {
            string[] old = { "body_seat", "body_crimson", "body_forest", "body_midnight", "body_mustard", "body_coal",
                             "body_motley", "body_gilded", "body_plum", "body_sky", "body_rose", "body_ash" };
            var ids = new List<string>();
            foreach (BodyColor c in BodyCatalog.Colors) ids.Add(c.Id);
            CollectionAssert.AreEqual(old, ids);

            BodyColor crimson = BodyCatalog.FindColor("body_crimson");
            Assert.AreEqual(0.55f, crimson.R, 1e-6f);
            Assert.AreEqual(0.11f, crimson.G, 1e-6f);
            Assert.AreEqual(0.12f, crimson.B, 1e-6f);
            Assert.IsTrue(BodyCatalog.FindColor(BodyCatalog.DefaultColorId).IsDefault);
        }

        [Test]
        public void OutfitAndColorIds_NeverOverlap()
        {
            foreach (BodyOutfit o in BodyCatalog.Outfits) Assert.IsNull(BodyCatalog.FindColor(o.Id), o.Id);
            foreach (BodyColor c in BodyCatalog.Colors) Assert.IsNull(BodyCatalog.FindOutfit(c.Id), c.Id);
        }

        // ------------------------------------------------------------------ loadout

        [Test]
        public void Default_IsBareInSeatColors()
        {
            BodyLoadout look = BodyLoadout.Default;
            Assert.IsTrue(look.IsBare);
            Assert.AreEqual(BodyCatalog.BareOutfitId, look.Outfit);
            Assert.AreEqual(BodyCatalog.DefaultColorId, look.Color);
        }

        [Test]
        public void ChangingOutfit_KeepsColor_AndChangingColor_KeepsOutfit()
        {
            var look = new BodyLoadout("outfit_tavern_vest", "body_crimson");

            BodyLoadout recoloured = look.WithColor("body_forest");
            Assert.AreEqual("outfit_tavern_vest", recoloured.Outfit);
            Assert.AreEqual("body_forest", recoloured.Color);

            BodyLoadout redressed = recoloured.WithOutfit("outfit_winter_fur_collar");
            Assert.AreEqual("outfit_winter_fur_collar", redressed.Outfit);
            Assert.AreEqual("body_forest", redressed.Color);

            BodyLoadout bare = redressed.WithOutfit(BodyCatalog.BareOutfitId);
            Assert.IsTrue(bare.IsBare);
            Assert.AreEqual("body_forest", bare.Color, "Bare keeps the chosen body colour");
        }

        [Test]
        public void UnknownWrongKindOrLockedIds_FallBack()
        {
            var look = new BodyLoadout("outfit_does_not_exist", "body_gilded");   // gilded is locked
            Assert.IsTrue(look.IsBare);
            Assert.AreEqual(BodyCatalog.DefaultColorId, look.Color);

            var swapped = new BodyLoadout("body_crimson", "outfit_tavern_vest");
            Assert.IsTrue(swapped.IsBare);
            Assert.AreEqual(BodyCatalog.DefaultColorId, swapped.Color);
        }

        [Test]
        public void LegacyProfile_WithColorUnderBody_KeepsTheColorAndGoesBare()
        {
            // Before outfits, "Body" held the colour; PlayerProfile.EquippedBody reads it as both.
            var look = new BodyLoadout("body_crimson", "body_crimson");
            Assert.IsTrue(look.IsBare);
            Assert.AreEqual("body_crimson", look.Color);
        }

        [Test]
        public void Reseat_LookFollowsTheParticipantNotTheSeat()
        {
            var saved = new BodyLoadout("outfit_gamblers_longcoat", "body_midnight");

            // Match 1: the local participant sits in seat 2. Rematch: reseated to seat 5.
            foreach (int localSeat in new[] { 2, 5 })
            {
                for (int seat = 0; seat < 6; seat++)
                {
                    BodyLoadout shown = BodyLoadout.ForParticipant(seat == localSeat, saved);
                    if (seat == localSeat)
                    {
                        Assert.AreEqual("outfit_gamblers_longcoat", shown.Outfit);
                        Assert.AreEqual("body_midnight", shown.Color);
                    }
                    else
                    {
                        Assert.IsTrue(shown.IsBare, "seat " + seat + " must not keep another participant's outfit");
                        Assert.AreEqual(BodyCatalog.DefaultColorId, shown.Color);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ assets

        [Test]
        public void ExportedOutfits_FitTheSharedBodyAnchor()
        {
            foreach (string name in ExportedOutfits)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(OutfitFolder + name + ".fbx");
                Assert.IsNotNull(go, name + " FBX missing");
                Assert.AreEqual(Vector3.zero, go.transform.localPosition, name);
                Assert.AreEqual(Vector3.one, go.transform.localScale, name);
                Assert.IsEmpty(go.GetComponentsInChildren<Collider>(true), name + " must carry no colliders");
                Assert.IsEmpty(go.GetComponentsInChildren<SkinnedMeshRenderer>(true), name + " must be a static torso shell");

                MeshFilter[] meshes = go.GetComponentsInChildren<MeshFilter>(true);
                Assert.IsNotEmpty(meshes, name);
                foreach (MeshFilter mf in meshes)
                {
                    // In the character's space: on the torso, between the chair seat and the
                    // neck, and narrower than the floating hands (no arms or sleeves).
                    Bounds b = WorldBounds(mf);
                    Assert.Greater(b.min.y, 0.45f, name + " " + mf.name + " bottom");
                    Assert.Less(b.max.y, 1.15f, name + " " + mf.name + " top");
                    Assert.Less(Mathf.Max(-b.min.x, b.max.x), 0.25f, name + " " + mf.name + " width");
                    Assert.Less(mf.sharedMesh.triangles.Length / 3, 10000, name + " " + mf.name + " triangles");
                }

                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m, name + " missing material");
                        StringAssert.StartsWith(OutfitFolder + "Materials/", AssetDatabase.GetAssetPath(m), name + " uses the shared outfit materials");
                    }
            }
        }

        [Test]
        public void TravellersWrap_HasNoSeparateHood()
        {
            // The hooded-wrap design was exported as one body shell. If a separate head-following
            // hood is exported later, it needs the head attachment and headwear suppression.
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(OutfitFolder + "TravellersWrap.fbx");
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                StringAssert.DoesNotContain("hood", t.name.ToLowerInvariant());
        }

        [Test]
        public void PlayerCharacter_HasAnEmptyBodyCosmeticAnchorAndTheOutfitLibrary()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Transform body = prefab.transform.Find("Body");
            Assert.IsNotNull(body);
            Transform anchor = body.Find("BodyCosmeticAnchor");
            Assert.IsNotNull(anchor, "Body/BodyCosmeticAnchor missing");
            Assert.AreEqual(Vector3.zero, anchor.localPosition);
            Assert.AreEqual(Quaternion.identity, anchor.localRotation);
            Assert.AreEqual(Vector3.one, anchor.localScale);
            Assert.AreEqual(0, anchor.childCount, "no outfit is baked into the prefab");
            Assert.AreEqual("BodyMesh", body.GetChild(0).name, "the body mesh stays the body's first renderer");

            MonoBehaviour character = null;
            foreach (MonoBehaviour mb in prefab.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "PlayerCharacter") character = mb;
            Assert.IsNotNull(character);
            var library = new SerializedObject(character).FindProperty("bodyOutfits").objectReferenceValue as ScriptableObject;
            Assert.IsNotNull(library, "PlayerCharacter has no BodyOutfitLibrary");

            SerializedProperty outfits = new SerializedObject(library).FindProperty("outfits");
            var names = new List<string>();
            for (int i = 0; i < outfits.arraySize; i++)
            {
                Object o = outfits.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null) names.Add(o.name);
            }
            CollectionAssert.AreEquivalent(ExportedOutfits, names);
        }

        static Bounds WorldBounds(MeshFilter mf)
        {
            // The asset root sits where the anchor puts it (origin, scale 1, its own FBX Z-up
            // correction), so the asset's world space is the anchor's space.
            Bounds mb = mf.sharedMesh.bounds;
            var b = new Bounds(mf.transform.TransformPoint(mb.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(mf.transform.TransformPoint(corner));
            }
            return b;
        }
    }
}
