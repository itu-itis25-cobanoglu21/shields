using System.Collections.Generic;

namespace Shields.Social
{
    /// <summary>Wardrobe tiers, in the order the wardrobe's own rarity list uses.</summary>
    public enum CosmeticRarity { Common, Rare, Legendary }

    /// <summary>
    /// One torso outfit: its id, display name and the exported FBX it comes from
    /// (file name without extension, under Art/Characters/Cosmetics/body_accessories).
    /// The bare outfit has no source asset.
    /// </summary>
    public sealed class BodyOutfit
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string SourceAsset;
        public readonly bool IsDefault;

        public BodyOutfit(string id, string name, string sourceAsset, bool isDefault)
        {
            Id = id;
            Name = name;
            SourceAsset = sourceAsset;
            IsDefault = isDefault;
        }
    }

    /// <summary>
    /// One base body colour: the tint the character's own body mesh takes. The default
    /// keeps the seat's placeholder colour. Colours are sRGB, as the material tint expects.
    /// </summary>
    public sealed class BodyColor
    {
        public readonly string Id;
        public readonly string Name;
        public readonly float R, G, B;
        public readonly bool Metal;     // tints the metal material instead of the cloth one
        public readonly CosmeticRarity Rarity;
        public readonly bool Locked;
        public readonly bool IsDefault;

        public BodyColor(string id, string name, float r, float g, float b, bool metal, CosmeticRarity rarity, bool locked, bool isDefault)
        {
            Id = id;
            Name = name;
            R = r;
            G = g;
            B = b;
            Metal = metal;
            Rarity = rarity;
            Locked = locked;
            IsDefault = isDefault;
        }
    }

    /// <summary>
    /// The BODY wardrobe: torso outfits and, independently, the base body colour.
    /// Cosmetic only: never part of the match.
    /// </summary>
    public static class BodyCatalog
    {
        public const string BareOutfitId = "outfit_bare";
        public const string DefaultColorId = "body_seat";

        /// <summary>Bare first, then the sixteen exported outfits.</summary>
        public static readonly IReadOnlyList<BodyOutfit> Outfits = new[]
        {
            new BodyOutfit(BareOutfitId, "BARE", null, true),
            new BodyOutfit("outfit_tavern_vest", "TAVERN VEST", "TavernVest", false),
            new BodyOutfit("outfit_gamblers_longcoat", "GAMBLER'S LONGCOAT", "GamblersLongcoat", false),
            new BodyOutfit("outfit_travellers_wrap", "TRAVELLER'S WRAP", "TravellersWrap", false),
            new BodyOutfit("outfit_merchant_robe", "MERCHANT ROBE", "MerchantRobeShell", false),
            new BodyOutfit("outfit_guard_chestpiece", "GUARD CHESTPIECE", "GuardChestpiece", false),
            new BodyOutfit("outfit_noble_doublet", "NOBLE DOUBLET", "NobleDoublet", false),
            new BodyOutfit("outfit_barkeep_apron", "BARKEEP APRON", "BarkeepApron", false),
            new BodyOutfit("outfit_occult_cardseer", "OCCULT CARDSEER", "OccultCardseerGarment", false),
            new BodyOutfit("outfit_patchwork_rogue", "PATCHWORK ROGUE", "PatchworkRogueJerkin", false),
            new BodyOutfit("outfit_minstrel_wrap", "MINSTREL WRAP", "TravelingMinstrelWrap", false),
            new BodyOutfit("outfit_brewers_harness", "BREWER'S HARNESS", "BrewersLeatherHarness", false),
            new BodyOutfit("outfit_monks_mantle", "MONK'S MANTLE", "MonksShoulderMantle", false),
            new BodyOutfit("outfit_duelists_jacket", "DUELIST'S JACKET", "DuelistsShortJacket", false),
            new BodyOutfit("outfit_ragged_outcast", "RAGGED OUTCAST", "RaggedOutcastWrap", false),
            new BodyOutfit("outfit_winter_fur_collar", "WINTER FUR COLLAR", "WinterFurCollarOutfit", false),
            new BodyOutfit("outfit_tavern_champion", "TAVERN CHAMPION", "TavernChampionOutfit", false),
        };

        /// <summary>
        /// The body colours that used to be BODY wardrobe cards, unchanged: same ids (so
        /// saved choices survive), names, colours, tiers and locks.
        /// </summary>
        public static readonly IReadOnlyList<BodyColor> Colors = new[]
        {
            new BodyColor(DefaultColorId, "SEAT COLORS", 0f, 0f, 0f, false, CosmeticRarity.Common, false, true),
            new BodyColor("body_crimson", "CRIMSON", 0.55f, 0.11f, 0.12f, false, CosmeticRarity.Common, false, false),
            new BodyColor("body_forest", "FOREST", 0.17f, 0.33f, 0.19f, false, CosmeticRarity.Common, false, false),
            new BodyColor("body_midnight", "MIDNIGHT", 0.14f, 0.18f, 0.36f, false, CosmeticRarity.Common, false, false),
            new BodyColor("body_mustard", "MUSTARD", 0.78f, 0.56f, 0.16f, false, CosmeticRarity.Rare, false, false),
            new BodyColor("body_coal", "COAL", 0.12f, 0.12f, 0.13f, false, CosmeticRarity.Rare, false, false),
            new BodyColor("body_motley", "JESTER MOTLEY", 0.42f, 0.16f, 0.46f, false, CosmeticRarity.Rare, true, false),
            new BodyColor("body_gilded", "GILDED", 0.86f, 0.62f, 0.2f, true, CosmeticRarity.Legendary, true, false),
            new BodyColor("body_plum", "PLUM", 0.36f, 0.14f, 0.3f, false, CosmeticRarity.Common, false, false),
            new BodyColor("body_sky", "SKY", 0.3f, 0.5f, 0.7f, false, CosmeticRarity.Common, false, false),
            new BodyColor("body_rose", "ROSE", 0.74f, 0.4f, 0.44f, false, CosmeticRarity.Common, false, false),
            new BodyColor("body_ash", "ASH", 0.48f, 0.47f, 0.45f, false, CosmeticRarity.Rare, true, false),
        };

        public static BodyOutfit FindOutfit(string id)
        {
            foreach (BodyOutfit o in Outfits) if (o.Id == id) return o;
            return null;
        }

        public static BodyColor FindColor(string id)
        {
            foreach (BodyColor c in Colors) if (c.Id == id) return c;
            return null;
        }

        /// <summary>The id if it names an outfit, otherwise Bare.</summary>
        public static string ResolveOutfit(string id)
        {
            return FindOutfit(id) != null ? id : BareOutfitId;
        }

        /// <summary>The id if it names an unlocked colour, otherwise the seat colours.</summary>
        public static string ResolveColor(string id)
        {
            BodyColor c = FindColor(id);
            return c != null && !c.Locked ? id : DefaultColorId;
        }
    }

    /// <summary>
    /// What one participant wears on the body: an outfit and, independently, the base body
    /// colour. Changing one never touches the other.
    /// </summary>
    public readonly struct BodyLoadout
    {
        public readonly string Outfit;
        public readonly string Color;

        /// <summary>Unknown ids fall back: the outfit to Bare, the colour (or a locked one) to seat colours.</summary>
        public BodyLoadout(string outfit, string color)
        {
            Outfit = BodyCatalog.ResolveOutfit(outfit);
            Color = BodyCatalog.ResolveColor(color);
        }

        public static BodyLoadout Default { get { return new BodyLoadout(null, null); } }

        public bool IsBare { get { return Outfit == BodyCatalog.BareOutfitId; } }

        public BodyLoadout WithOutfit(string outfit) { return new BodyLoadout(outfit, Color); }
        public BodyLoadout WithColor(string color) { return new BodyLoadout(Outfit, color); }

        /// <summary>
        /// The look a seated character shows. Cosmetics belong to the participant, not the
        /// seat: only this machine's own participant wears the saved loadout, wherever they
        /// sit; everyone else (bots, remote players, empty seats) shows the default, because
        /// cosmetics are not networked.
        /// </summary>
        public static BodyLoadout ForParticipant(bool isLocalParticipant, BodyLoadout saved)
        {
            return isLocalParticipant ? saved : Default;
        }
    }
}
