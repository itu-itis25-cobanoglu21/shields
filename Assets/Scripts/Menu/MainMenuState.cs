using System;
using System.Collections.Generic;
using Shields.Cards;
using Shields.Game;
using Shields.Settings;
using UnityEngine;

namespace Shields.Menu
{
    public enum MenuScreen { MainMenu, Customize, Settings }
    public enum MenuPanel { None, Offline, Join, Create }
    public enum CustomizeTab { Wardrobe, Shop }
    public enum WardrobeSlot { Head, Body, Hands }
    public enum ItemRarity { Common, Rare, Legendary }

    /// <summary>One wardrobe entry. Placeholder cosmetics built from existing props and materials.</summary>
    public sealed class CosmeticItem
    {
        public string Id;
        public string Name;
        public WardrobeSlot Slot;
        public ItemRarity Rarity;
        public bool IsDefault;
        public bool Locked;

        // Preview data: a head prop prefab, or a material for body / hands.
        // A head item with both is the prop recoloured.
        public GameObject HeadPrefab;
        public Material Material;

        // Rendered at startup by the thumbnail studio.
        public Texture2D Thumbnail;
    }

    /// <summary>
    /// Everything the main menu shows: which screen and panel is open, the table setup
    /// for Offline / Create Room, and the wardrobe. The view reads it; the controller and
    /// the view change it and call <see cref="Notify"/>. Equipped cosmetics are saved
    /// through <see cref="PlayerProfile"/>.
    /// </summary>
    public sealed class MainMenuState
    {
        public event Action Changed;

        public MenuScreen Screen = MenuScreen.MainMenu;
        public MenuPanel Panel = MenuPanel.None;
        public string Message = string.Empty;   // shown in the open panel (empty code, load failure)
        public bool Busy;                       // a scene is loading; the menu ignores input

        // Table setup for Offline / Create Room. Defaults match MatchBootstrap.
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
        readonly Dictionary<WardrobeSlot, string> _equipped = new Dictionary<WardrobeSlot, string>();
        readonly Dictionary<WardrobeSlot, string> _selected = new Dictionary<WardrobeSlot, string>();

        public void Notify() { Changed?.Invoke(); }

        // ------------------------------------------------------------------ wardrobe

        /// <summary>Restores the saved equipment once the catalog is filled; unknown or locked ids fall back to the default.</summary>
        public void LoadEquipped()
        {
            foreach (WardrobeSlot slot in Enum.GetValues(typeof(WardrobeSlot)))
            {
                CosmeticItem def = DefaultFor(slot);
                CosmeticItem saved = Find(PlayerProfile.GetEquipped(slot.ToString(), def != null ? def.Id : string.Empty));
                if (saved == null || saved.Slot != slot || saved.Locked) saved = def;
                string id = saved != null ? saved.Id : string.Empty;
                _equipped[slot] = id;
                _selected[slot] = id;
            }
        }

        public List<CosmeticItem> ItemsIn(WardrobeSlot slot)
        {
            var list = new List<CosmeticItem>();
            foreach (CosmeticItem item in Items) if (item.Slot == slot) list.Add(item);
            return list;
        }

        public CosmeticItem Find(string id)
        {
            foreach (CosmeticItem item in Items) if (item.Id == id) return item;
            return null;
        }

        public CosmeticItem DefaultFor(WardrobeSlot slot)
        {
            foreach (CosmeticItem item in Items) if (item.Slot == slot && item.IsDefault) return item;
            return null;
        }

        public CosmeticItem SelectedItem(WardrobeSlot slot) { return Find(_selected[slot]); }
        public CosmeticItem EquippedItem(WardrobeSlot slot) { return Find(_equipped[slot]); }

        public bool IsEquipped(CosmeticItem item) { return _equipped[item.Slot] == item.Id; }
        public bool IsSelected(CosmeticItem item) { return _selected[item.Slot] == item.Id; }

        public void Select(CosmeticItem item)
        {
            if (item == null) return;
            _selected[item.Slot] = item.Id;
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
            _equipped[Slot] = _selected[Slot];
            PlayerProfile.SetEquipped(Slot.ToString(), _equipped[Slot]);
            Notify();
        }

        public void ResetSlotToDefault()
        {
            CosmeticItem def = DefaultFor(Slot);
            if (def == null) return;
            _equipped[Slot] = def.Id;
            _selected[Slot] = def.Id;
            PlayerProfile.SetEquipped(Slot.ToString(), def.Id);
            Notify();
        }

        /// <summary>Drops any try-on so every slot shows what is equipped again.</summary>
        public void ClearTryOn()
        {
            foreach (WardrobeSlot slot in Enum.GetValues(typeof(WardrobeSlot))) _selected[slot] = _equipped[slot];
        }

        // ------------------------------------------------------------------ text

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

        public static string Percent(float value) { return Mathf.RoundToInt(value * 100f).ToString(); }
        public static string Sensitivity(float value) { return value.ToString("0.00"); }
    }
}
