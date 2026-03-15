using VtmTool.Core.Enums;

namespace VtmTool.Core.Models;

/// <summary>
/// Represents a character in the game, including identity, clan, generation, attributes, skills, health, willpower,
/// and hunger.
/// </summary>
/// <remarks>The Character struct encapsulates all core statistics and state for a single game character,
/// including physical, social, and mental attributes and skills, as well as health, willpower, and hunger levels.
/// Generation and blood potency are subject to game-specific constraints. Health and willpower damage values must
/// not exceed their respective maximums, which are derived from attribute values. This struct is intended for use
/// in systems modeling character state and progression.</remarks>
public struct Character
{
    public int Id;
    public string Name;
    public Clan Clan;
    public byte Generation; // 4-16
    public byte BloodPotency; // 0-10, capped by Generation
    public byte Humanity; // 0–10, starts at 7

    // Attributes
    public byte Strength, Dexterity, Stamina; // Physical Attributes
    public byte Charisma, Manipulation, Composure; // Social Attributes
    public byte Intelligence, Wits, Resolve; // Mental Attributes

    // Skills
    public byte Athletics, Brawl, Craft, Drive, Firearms,
        Larceny, Melee, Stealth, Survival; // Physical Skills
    public byte AnimalKen, Etiquette, Insight, Intimidation,
        Leadership, Performance, Persuasion, Streetwise, Subterfuge; // Social Skills
    public byte Academics, Awareness, Finance, Investigation,
        Medicine, Occult, Politics, Science, Technology; // Mental Skills

    // Health
    // Max is derived; only damage is stored.
    // Total damage (Agg + Superficial) must not exceed HealthMax.
    public byte AggravatedHealth;
    public byte SuperficialHealth;

    // Willpower
    public byte AggravatedWillpower;
    public byte SuperficialWillpower;

    // Hunger
    public byte Hunger; // 0–5

    public readonly byte HealthMax => (byte)(Stamina + 3);
    public readonly byte WillpowerMax => (byte)(Composure + Resolve);
}