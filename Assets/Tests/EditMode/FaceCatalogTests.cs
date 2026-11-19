using System.Collections.Generic;
using NUnit.Framework;
using Shields.Social;

namespace Shields.Game.Tests
{
    /// <summary>
    /// Face cosmetics: three slots, facial hair as Mouth options (so a mouth and a beard can
    /// never stack), one default per slot, and saved ids that fall back safely.
    /// </summary>
    public class FaceCatalogTests
    {
        static int Count(FaceSlot slot)
        {
            int n = 0;
            foreach (FaceCosmetic f in FaceCatalog.All) if (f.Slot == slot) n++;
            return n;
        }

        [Test]
        public void Catalog_HasSixEyesSixEyebrowsTenMouths()
        {
            Assert.AreEqual(6, Count(FaceSlot.Eyes));
            Assert.AreEqual(6, Count(FaceSlot.Eyebrows));
            Assert.AreEqual(10, Count(FaceSlot.Mouth));
            Assert.AreEqual(22, FaceCatalog.All.Count);
        }

        [Test]
        public void FacialHair_IsAlwaysAMouthOption()
        {
            int hair = 0;
            foreach (FaceCosmetic f in FaceCatalog.All)
            {
                if (!f.IsFacialHair) continue;
                hair++;
                Assert.AreEqual(FaceSlot.Mouth, f.Slot, f.Id);
            }
            Assert.AreEqual(4, hair);
        }

        [Test]
        public void Ids_AndSourceMeshes_AreUnique()
        {
            var ids = new HashSet<string>();
            var meshes = new HashSet<string>();
            foreach (FaceCosmetic f in FaceCatalog.All)
            {
                Assert.IsTrue(ids.Add(f.Id), "duplicate id " + f.Id);
                Assert.IsTrue(meshes.Add(f.SourceMesh), "duplicate mesh " + f.SourceMesh);
            }
        }

        [Test]
        public void EverySlot_HasExactlyOneDefault()
        {
            foreach (FaceSlot slot in new[] { FaceSlot.Eyes, FaceSlot.Eyebrows, FaceSlot.Mouth })
            {
                int defaults = 0;
                foreach (FaceCosmetic f in FaceCatalog.All) if (f.Slot == slot && f.IsDefault) defaults++;
                Assert.AreEqual(1, defaults, slot.ToString());
                Assert.IsFalse(FaceCatalog.DefaultFor(slot).IsFacialHair);
            }
        }

        [Test]
        public void Loadout_KeepsValidIds()
        {
            var face = new FaceLoadout("eyes_sleepy", "brows_surprised", "mouth_poker_face");
            Assert.AreEqual("eyes_sleepy", face.Eyes);
            Assert.AreEqual("brows_surprised", face.Eyebrows);
            Assert.AreEqual("mouth_poker_face", face.Mouth);
        }

        [Test]
        public void Loadout_FacialHair_ReplacesTheMouth()
        {
            var face = new FaceLoadout("eyes_smug", "brows_furrowed", "mouth_curled_mustache");
            Assert.AreEqual("mouth_curled_mustache", face.Mouth);
            Assert.AreEqual("mouth_curled_mustache", face.IdFor(FaceSlot.Mouth));
        }

        [Test]
        public void Loadout_UnknownOrMissingIds_FallBackToDefaults()
        {
            var face = new FaceLoadout(null, "", "mouth_does_not_exist");
            Assert.AreEqual(FaceCatalog.DefaultFor(FaceSlot.Eyes).Id, face.Eyes);
            Assert.AreEqual(FaceCatalog.DefaultFor(FaceSlot.Eyebrows).Id, face.Eyebrows);
            Assert.AreEqual(FaceCatalog.DefaultFor(FaceSlot.Mouth).Id, face.Mouth);
        }

        [Test]
        public void Loadout_IdFromAnotherSlot_FallsBackToDefault()
        {
            var face = new FaceLoadout("mouth_stubby_beard", "eyes_bluffing", "brows_skeptical");
            Assert.AreEqual(FaceCatalog.DefaultFor(FaceSlot.Eyes).Id, face.Eyes);
            Assert.AreEqual(FaceCatalog.DefaultFor(FaceSlot.Eyebrows).Id, face.Eyebrows);
            Assert.AreEqual(FaceCatalog.DefaultFor(FaceSlot.Mouth).Id, face.Mouth);
        }

        [Test]
        public void DefaultLoadout_IsTheCatalogDefaults()
        {
            FaceLoadout face = FaceLoadout.Default;
            Assert.AreEqual("eyes_suspicious", face.Eyes);
            Assert.AreEqual("brows_mischievous", face.Eyebrows);
            Assert.AreEqual("mouth_crooked_grin", face.Mouth);
        }
    }
}
