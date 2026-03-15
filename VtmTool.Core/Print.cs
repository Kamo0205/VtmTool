using System;
using VtmTool.Core.Enums;
using VtmTool.Core.Models;

namespace VtmTool.Core;

/// <summary>
/// Provides static helper methods for formatting and displaying character information, ratings, and tracks in a
/// text-based interface.
/// </summary>
/// <remarks>The Print class includes methods for rendering dot ratings, damage and hunger tracks, and for
/// printing a full character sheet to the console. These methods are intended for use in console-based applications and
/// are tailored to the formatting conventions of the Vampire: The Masquerade 5th Edition system. All members are static
/// and thread safety is not guaranteed for console output operations.</remarks>
public static class Print
{
    // Filled/empty dot rating. e.g. Dots(3) → "●●●○○"
    public static string Dots(byte value, byte max = 5) =>
        new string('\u25CF', Math.Min(value, max)) +
        new string('\u25CB', Math.Max(max - value, 0));

    // Damage track: [X] = Aggravated, [/] = Superficial, [ ] = empty
    public static string DamageTrack(byte agg, byte sup, byte max)
    {
        var sb = new System.Text.StringBuilder(max * 3);
        for (int i = 0; i < max; i++)
        {
            if (i < agg) sb.Append("[X]");
            else if (i < agg + sup) sb.Append("[/]");
            else sb.Append("[ ]");
        }
        return sb.ToString();
    }

    // Hunger track: [H] = hungry, [ ] = sated
    public static string HungerTrack(byte hunger)
    {
        var sb = new System.Text.StringBuilder(15);
        for (int i = 0; i < 5; i++)
            sb.Append(i < hunger ? "[H]" : "[ ]");
        return sb.ToString();
    }

    static void SectionHeader(string title) =>
        Console.WriteLine($"\n  \x1b[0;33m── {title} ──\x1b[0m");

    static string ClanName(Clan c) => c switch
    {
        Clan.Banu_Haqim => "Banu Haqim",
        _ => c.ToString()
    };

    // Full V5 character sheet.
    public static void Sheet(in Character c)
    {
        Console.WriteLine();
        Console.WriteLine($"  \x1b[1m{c.Name}\x1b[0m");
        Console.WriteLine($"  {ClanName(c.Clan)}  ·  {c.Generation}th Generation  ·  Humanity {Dots(c.Humanity, 10)}");
        Console.WriteLine($"  Blood Potency {Dots(c.BloodPotency)}  (max {Rules.MaxBloodPotency(c.Generation)})");

        SectionHeader("ATTRIBUTES");
        Console.WriteLine($"  {"PHYSICAL",-26} {"SOCIAL",-26} MENTAL");
        Console.WriteLine($"  {"Strength",-14} {Dots(c.Strength),-10}  {"Charisma",-14} {Dots(c.Charisma),-10}  {"Intelligence",-14} {Dots(c.Intelligence)}");
        Console.WriteLine($"  {"Dexterity",-14} {Dots(c.Dexterity),-10}  {"Manipulation",-14} {Dots(c.Manipulation),-10}  {"Wits",-14} {Dots(c.Wits)}");
        Console.WriteLine($"  {"Stamina",-14} {Dots(c.Stamina),-10}  {"Composure",-14} {Dots(c.Composure),-10}  {"Resolve",-14} {Dots(c.Resolve)}");

        SectionHeader("SKILLS");
        Console.WriteLine($"  {"PHYSICAL",-26} {"SOCIAL",-26} MENTAL");
        var p = new (string n, byte v)[] {
                ("Athletics",  c.Athletics),  ("Brawl",        c.Brawl),
                ("Craft",      c.Craft),      ("Drive",        c.Drive),
                ("Firearms",   c.Firearms),   ("Larceny",      c.Larceny),
                ("Melee",      c.Melee),      ("Stealth",      c.Stealth),
                ("Survival",   c.Survival),
            };
        var s = new (string n, byte v)[] {
                ("Animal Ken", c.AnimalKen),  ("Etiquette",    c.Etiquette),
                ("Insight",    c.Insight),    ("Intimidation", c.Intimidation),
                ("Leadership", c.Leadership), ("Performance",  c.Performance),
                ("Persuasion", c.Persuasion), ("Streetwise",   c.Streetwise),
                ("Subterfuge", c.Subterfuge),
            };
        var m = new (string n, byte v)[] {
                ("Academics",  c.Academics),  ("Awareness",    c.Awareness),
                ("Finance",    c.Finance),    ("Investigation",c.Investigation),
                ("Medicine",   c.Medicine),   ("Occult",       c.Occult),
                ("Politics",   c.Politics),   ("Science",      c.Science),
                ("Technology", c.Technology),
            };
        for (int i = 0; i < 9; i++)
        {
            string pc = $"{p[i].n,-14} {Dots(p[i].v)}";
            string sc = $"{s[i].n,-14} {Dots(s[i].v)}";
            string mc = $"{m[i].n,-14} {Dots(m[i].v)}";
            Console.WriteLine($"  {pc,-26}  {sc,-26}  {mc}");
        }

        SectionHeader("HEALTH & WILLPOWER");
        int wp = Rules.WoundPenalty(c);
        string wpNote = wp < 0 ? $"  \x1b[0;31mWound penalty {wp}\x1b[0m" : "";
        Console.WriteLine($"  Health    ({c.HealthMax})  {DamageTrack(c.AggravatedHealth, c.SuperficialHealth, c.HealthMax)}{wpNote}");
        Console.WriteLine($"  Willpower ({c.WillpowerMax})  {DamageTrack(c.AggravatedWillpower, c.SuperficialWillpower, c.WillpowerMax)}");

        SectionHeader("HUNGER");
        Console.WriteLine($"  {HungerTrack(c.Hunger)}  {c.Hunger}/5");
        Console.WriteLine();
    }

    public static void Help(bool inCharacter)
    {
        Console.WriteLine();
        if (inCharacter)
        {
            Console.WriteLine("  sheet        — print full character sheet");
            Console.WriteLine("  edit attr    — reassign attribute dots");
            Console.WriteLine("  edit skills  — reassign skill dots");
            Console.WriteLine("  rouse        — perform a Rouse check (V5 p.212)");
            Console.WriteLine("  damage       — apply health or willpower damage");
            Console.WriteLine("  heal         — recover superficial damage");
            Console.WriteLine("  back         — return to main menu");
        }
        else
        {
            Console.WriteLine("  new          — create a new character");
            Console.WriteLine("  list         — list all characters");
            Console.WriteLine("  load         — load a character by name");
            Console.WriteLine("  delete       — delete a character by name");
            Console.WriteLine("  help         — show this list");
            Console.WriteLine("  quit         — exit");
        }
        Console.WriteLine();
    }
}