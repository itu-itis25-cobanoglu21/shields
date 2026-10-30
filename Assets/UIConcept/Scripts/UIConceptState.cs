using System;
using System.Collections.Generic;
using Shields.Cards;
using Shields.Game;
using UnityEngine;

namespace Shields.UIConcept
{
    // Prototype-only UI state shared by all three concepts, so switching
    // concept keeps the same screen, tab, selection and settings values.

    public enum ConceptScreen { MainMenu, Customize, Settings }
    public enum MenuPanel { None, Offline, Join, Create }
    public enum CustomizeTab { Wardrobe, Shop }
    public enum WardrobeSlot { Head, Body, Hands }
    public enum ItemRarity { Common, Rare, Legendary }

    public sealed class CosmeticItem
    {
        public string Id;
        public string Name;
        public WardrobeSlot Slot;
        public ItemRarity Rarity;
        public bool IsDefault;
        public bool Locked;

        // Extra placeholders that only exist to make scrolling testable.
        public bool Extra;

        // Preview data: a head prop prefab, or a material for body / hands.
        // A head item with both a prefab and a material is the prop recoloured.
        public GameObject HeadPrefab;
        public Material Material;

        // Filled at startup by the thumbnail studio. Null for locked items without a model.
        public Texture2D Thumbnail;
    }

    public sealed class UIConceptState
    {
        public event Action Changed;

        public ConceptScreen Screen = ConceptScreen.MainMenu;
        // The main menu panel unfolded under its entry (Offline, Join Room or Create Room).
        public MenuPanel Panel = MenuPanel.None;
        public string JoinMessage = string.Empty;

        // Table setup shared by the Offline and Create Room panels. Defaults match the
        // production offline setup (MatchBootstrap): six players, one deck.
        public int TablePlayers = 6;
        public int TableDecks = 1;

        public const int MinPlayers = MatchConfig.MinPlayers;
        public const int MaxPlayers = MatchConfig.MaxPlayers;
        public const int MinDecks = CardDeck.MinDeckCount;
        public const int MaxDecks = CardDeck.MaxDeckCount;
        public const int CardsPerDeck = CardDeck.CardsPerStandardDeck;

        public CustomizeTab Tab = CustomizeTab.Wardrobe;
        public WardrobeSlot Slot = WardrobeSlot.Head;

        public readonly List<CosmeticItem> Items = new List<CosmeticItem>();
        public readonly Dictionary<WardrobeSlot, string> Equipped = new Dictionary<WardrobeSlot, string>();
        public readonly Dictionary<WardrobeSlot, string> Selected = new Dictionary<WardrobeSlot, string>();

        public string Nickname = "GOBLET GAMBLER";

        public float MasterVolume = 0.8f;
        public float MusicVolume = 0.55f;
        public float EffectsVolume = 0.75f;
        public float MouseSensitivity = 1.0f; // 0.1 .. 3.0
        public int ResolutionIndex = 2;
        public bool Fullscreen = true;
        public bool VSync = true;

        // Settings category, used by concepts that tab their settings.
        public int SettingsPage;

        public static readonly string[] Resolutions =
        {
            "1280 × 720", "1600 × 900", "1920 × 1080", "2560 × 1440", "3840 × 2160"
        };

        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 3f;

        public void Notify() { Changed?.Invoke(); }

        public IEnumerable<CosmeticItem> ItemsIn(WardrobeSlot slot)
        {
            foreach (CosmeticItem item in Items)
                if (item.Slot == slot) yield return item;
        }

        public CosmeticItem Find(string id)
        {
            foreach (CosmeticItem item in Items)
                if (item.Id == id) return item;
            return null;
        }

        public CosmeticItem DefaultFor(WardrobeSlot slot)
        {
            foreach (CosmeticItem item in Items)
                if (item.Slot == slot && item.IsDefault) return item;
            return null;
        }

        public CosmeticItem SelectedItem(WardrobeSlot slot) { return Find(Selected[slot]); }
        public CosmeticItem EquippedItem(WardrobeSlot slot) { return Find(Equipped[slot]); }

        public bool IsEquipped(CosmeticItem item) { return Equipped[item.Slot] == item.Id; }
        public bool IsSelected(CosmeticItem item) { return Selected[item.Slot] == item.Id; }

        public void Select(CosmeticItem item)
        {
            if (item == null) return;
            Selected[item.Slot] = item.Id;
            Notify();
        }

        public bool CanEquipSelected()
        {
            CosmeticItem item = SelectedItem(Slot);
            return item != null && !item.Locked && !IsEquipped(item);
        }

        public void EquipSelected()
        {
            if (!CanEquipSelected()) return;
            Equipped[Slot] = Selected[Slot];
            Notify();
        }

        public void ResetSlotToDefault()
        {
            CosmeticItem def = DefaultFor(Slot);
            if (def == null) return;
            Equipped[Slot] = def.Id;
            Selected[Slot] = def.Id;
            Notify();
        }

        public static string Percent(float value) { return Mathf.RoundToInt(value * 100f).ToString(); }
        public static string Sensitivity(float value) { return value.ToString("0.00"); }

        public static string SlotName(WardrobeSlot slot)
        {
            switch (slot)
            {
                case WardrobeSlot.Head: return "HEAD";
                case WardrobeSlot.Body: return "BODY";
                default: return "HANDS";
            }
        }

        public static string RarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return "RARE";
                case ItemRarity.Legendary: return "LEGENDARY";
                default: return "COMMON";
            }
        }
    }
}
