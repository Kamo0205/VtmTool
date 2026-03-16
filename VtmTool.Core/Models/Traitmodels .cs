using System.Collections.Generic;

namespace VtmTool.Core.Models;

// ── TraitCategory ─────────────────────────────────────────────────────────────
public enum TraitCategory : byte
{
    Background = 0,
    Merit = 1,
    Flaw = 2,
}

// ── CharacterTrait ────────────────────────────────────────────────────────────
// One row in the trait table. Backgrounds, Merits, and Flaws all share this
// struct — category distinguishes them.
public struct CharacterTrait
{
    public int Id;
    public int CharacterId;
    public TraitCategory Category;
    public string Name;
    public byte Rating;    // 0 for unrated Flaws, 1–5 otherwise
    public string Detail;    // free text, e.g. "Warehouse in the Docks"
    public bool IsCustom;  // true if not from the fixed list
}

// ── TraitDefinitions ──────────────────────────────────────────────────────────
// Fixed reference data. Never stored in the DB — only used to populate
// the "add trait" dropdown. Custom entries bypass this list entirely.
//
// V5 corebook p.178–190.
public static class TraitDefinitions
{
    public static readonly string[] Backgrounds = new[]
    {
        "Allies", "Contacts", "Fame", "Haven", "Herd",
        "Influence", "Loresheet", "Mask", "Mawla",
        "Resources", "Retainers", "Status",
    };

    public static readonly string[] Merits = new[]
    {
        "Beautiful", "Bloodhound", "Bonds of Fraternity", "Calm Heart",
        "Cobbler", "Cold Hearted", "Eat Food", "Efficient Killer",
        "Eerie Presence", "Feeding Grounds", "Fleet of Foot", "Fresh Start",
        "Greater Predator", "Language", "Looks", "Thin-Blood Alchemist",
        "Touch of Elegance", "Toll of the Ages", "Unbondable", "Untouchable",
    };

    public static readonly string[] Flaws = new[]
    {
        "Addiction", "Archaic", "Baby Teeth", "Bestial Temptation",
        "Creature of the Night", "Dark Arrogance", "Enemy",
        "Hard of Hearing", "Haunted", "Infamy", "Living in the Past",
        "Looks", "Monstrous", "Obvious Predator", "Prey Exclusion",
        "Repelled by Crosses", "Short Fuse", "Stake Bait", "True Love",
        "Weak Bloodline",
    };

    // XP cost to raise a Background by one dot (V5 p.191)
    // Cost = new rating × 3
    public static int BackgroundXpCost(int toRating) => toRating * 3;

    // Merits and Flaws are typically purchased at creation with Freebie points,
    // not raised with XP — but allow ST override at cost 3 per dot.
    public static int MeritXpCost(int toRating) => toRating * 3;
}