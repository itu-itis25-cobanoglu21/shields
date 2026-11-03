using System.Collections.Generic;
using Shields.Game;
using Shields.Match;
using Shields.Online;
using Shields.Presentation;
using UnityEngine;

namespace Shields.UI
{
    public enum NoteKind { Neutral, Good, Bad, Big }

    /// <summary>One line in the notification feed, written for players (seats numbered from 1).</summary>
    public sealed class HudNote
    {
        public string Headline;
        public string Detail;
        public NoteKind Kind;
        public float Time;

        // Set for Attacks and Charged Attacks, so a concept can draw its own line.
        public bool IsAttack;
        public bool Charged;
        public int Actor, Target;
        public int Power, Shield, Damage;
        public bool ChargeBroken;
    }

    /// <summary>
    /// Everything the in-game HUD shows, rebuilt every frame from public match state only
    /// (the same snapshot the prototype IMGUI HUD draws). Presentation only.
    /// </summary>
    public sealed class HudModel
    {
        public bool InMatch;            // a match snapshot is on screen
        public bool ChoosingShield;     // the local player still has to pick a Shield
        public bool WaitingForSetup;    // setup is running but the local choice is made
        public PublicMatchSnapshot Snap;
        public int Local = -1;
        public HudPendingAction Pending;
        public int Hovered = -1;
        public int Current = -1;
        public float TurnStartedAt;

        public bool MatchOver;
        public int Winner = -1;
        public bool MyTurn;
        public bool LocalAlive;
        public bool LocalHasCharge;

        public bool OrderAvailable, OrderOpen, CanDrink, Drinking, RematchVisible;

        // Online rematch vote: everyone human must agree.
        public bool RematchOnline, RematchVoted;
        public int RematchYes, RematchNeeded;

        // The host's latest refusal (online), null when none is showing.
        public string Refusal;
        public float RefusalAlpha;

        public readonly List<HudNote> Notes = new List<HudNote>();
        public const int MaxNotes = 6;

        int _lastGeneration = -1;

        public int PlayerCount { get { return InMatch ? Snap.PlayerCount : 0; } }
        public PublicPlayerState Player(int i) { return Snap.GetPlayer(i); }

        // ------------------------------------------------------------------ names

        public string Name(int i)
        {
            if (i < 0) return "NOBODY";
            return i == Local ? "YOU" : "PLAYER " + (i + 1);
        }

        public string Possessive(int i)
        {
            if (i == Local) return "YOUR";
            return Name(i) + "'S";
        }

        public string ActionName(HudPendingAction action)
        {
            switch (action)
            {
                case HudPendingAction.Attack: return "ATTACK";
                case HudPendingAction.ReplaceShield: return "REPLACE SHIELD";
                case HudPendingAction.ChargedAttack: return "CHARGED ATTACK";
                default: return string.Empty;
            }
        }

        /// <summary>The third action: Charge, or Charged Attack once a Charge card is stored.</summary>
        public string ThirdAction { get { return LocalHasCharge ? "CHARGED ATTACK" : "CHARGE"; } }

        // ------------------------------------------------------------------ update

        public void SetMatch(PublicMatchSnapshot snap, int local, HudPendingAction pending, int hovered)
        {
            bool newMatch = !InMatch || snap.Generation != _lastGeneration;
            InMatch = true;
            Snap = snap;
            Local = local;
            Pending = pending;
            Hovered = hovered;
            MatchOver = snap.MatchOver;
            Winner = snap.WinnerIndex;

            if (newMatch)
            {
                _lastGeneration = snap.Generation;
                Notes.Clear();
                Current = -1;
                string who = snap.CurrentPlayerIndex == local ? "YOU START" : Name(snap.CurrentPlayerIndex) + " STARTS";
                Add(who, snap.Direction == TurnDirection.Clockwise ? "TURNS GO CLOCKWISE" : "TURNS GO COUNTER-CLOCKWISE", NoteKind.Neutral);
            }

            if (snap.CurrentPlayerIndex != Current)
            {
                Current = snap.CurrentPlayerIndex;
                TurnStartedAt = Time.unscaledTime;
            }

            PublicPlayerState me = snap.GetPlayer(local);
            LocalAlive = local >= 0 && me.IsAlive;
            LocalHasCharge = local >= 0 && me.HasCharge;
            MyTurn = !MatchOver && LocalAlive && Current == local;
        }

        public void ClearMatch()
        {
            InMatch = false;
            MatchOver = false;
            MyTurn = false;
            Current = -1;
        }

        public HudNote Add(string headline, string detail, NoteKind kind)
        {
            var note = new HudNote { Headline = headline, Detail = detail, Kind = kind, Time = Time.unscaledTime };
            Notes.Add(note);
            while (Notes.Count > MaxNotes) Notes.RemoveAt(0);
            return note;
        }

        static void SetAttack(HudNote note, PublicActionResult r)
        {
            note.IsAttack = true;
            note.Charged = r.IsChargedAttack;
            note.Actor = r.ActorIndex;
            note.Target = r.TargetIndex;
            note.Power = r.AttackPower;
            note.Shield = r.TargetShieldValue;
            note.Damage = r.Damage;
            note.ChargeBroken = r.TargetChargeInterrupted;
        }

        /// <summary>Turns a resolved action into player-facing lines.</summary>
        public void AddResult(PublicActionResult r, int local)
        {
            Local = local;
            int a = r.ActorIndex, t = r.TargetIndex;
            NoteKind kind = NoteKind.Neutral;
            if (r.Damage > 0 && t == local) kind = NoteKind.Bad;
            else if (r.Damage > 0 && a == local) kind = NoteKind.Good;

            switch (r.ActionType)
            {
                case TurnActionType.Attack:
                    SetAttack(Add(Name(a) + (a == local ? " ATTACK " : " ATTACKS ") + (t == local ? "YOU" : Name(t)), Outcome(r), kind), r);
                    break;

                case TurnActionType.ReplaceShield:
                    string whose = t == a ? (a == local ? "YOUR" : "THEIR") : Possessive(t);
                    Add(Name(a) + (a == local ? " REPLACE " : " REPLACES ") + whose + " SHIELD",
                        "WAS " + Face(r.OldShieldCard) + "  ·  NOW " + Face(r.NewShieldCard), NoteKind.Neutral);
                    break;

                default:
                    if (r.IsChargedAttack)
                        SetAttack(Add(Name(a) + (a == local ? " FIRE" : " FIRES") + " A CHARGED ATTACK AT " + (t == local ? "YOU" : Name(t)), Outcome(r), kind), r);
                    else
                        Add(Name(a) + (a == local ? " STORE " : " STORES ") + "A CHARGE", "ONE CARD FACE DOWN", NoteKind.Neutral);
                    break;
            }

            if (r.TargetEliminated)
                Add(t == local ? "YOU ARE OUT" : Name(t) + " IS OUT", "ELIMINATED BY " + Name(a), NoteKind.Big);
        }

        string Outcome(PublicActionResult r)
        {
            string power;
            if (r.IsChargedAttack && r.RevealedCardCount == 2)
                power = Face(r.RevealedCards[0]) + " + " + Face(r.RevealedCards[1]) + " = " + r.AttackPower;
            else if (r.RevealedCardCount == 1)
                power = Face(r.RevealedCards[0]) + (Face(r.RevealedCards[0]) == r.AttackPower.ToString() ? "" : " (" + r.AttackPower + ")");
            else
                power = r.AttackPower.ToString();

            string outcome = r.Damage > 0 ? r.Damage + " DAMAGE" : "BLOCKED";
            if (r.TargetChargeInterrupted) outcome += "  ·  CHARGE BROKEN";
            return power + " VS SHIELD " + r.TargetShieldValue + "  ·  " + outcome;
        }

        static string Face(PublicCard card)
        {
            return card.IsValid ? CardView.RankSymbol(card.Rank) : "?";
        }
    }
}
