using System;
using System.Collections.Generic;
using VtmTool.Core.Models;

namespace VtmTool.Core;

// =========================================================================
// Creation
//
// Guided V5 character creation steps. Each method takes ref Character and
// writes the chosen values directly in. Called in sequence from NewCharacter.
// =========================================================================
public static class Creation
{
    // Read an integer in [min, max], re-prompting until valid.
    static int ReadInt(string prompt, int min, int max)
    {
        while (true)
        {
            Console.Write(prompt);
            if (int.TryParse(Console.ReadLine()?.Trim(), out int v) && v >= min && v <= max)
                return v;
            Console.WriteLine($"    Enter a number between {min} and {max}.");
        }
    }

    // Read "physical", "social", or "mental", rejecting already-used choices.
    static string ReadCategory(string prompt, HashSet<string> used)
    {
        while (true)
        {
            Console.Write(prompt);
            string? line = Console.ReadLine()?.Trim().ToLower();
            if (line is "physical" or "social" or "mental")
            {
                if (used.Contains(line)) Console.WriteLine($"    '{line}' already chosen.");
                else return line;
            }
            else Console.WriteLine("    Enter: physical, social, or mental.");
        }
    }

    // Prompt nine skill values for one category with a fixed budget.
    // Each value is in [min, max] and the sum must equal budget exactly.
    static byte[] PromptNineDots(string catLabel, string[] names,
                                 int budget, int min, int max)
    {
        Console.WriteLine($"\n  {catLabel.ToUpperInvariant()} — assign {budget} dots total (each {min}–{max}):");
        while (true)
        {
            var vals = new int[9];
            for (int i = 0; i < 9; i++)
                vals[i] = ReadInt($"    {names[i],-16}: ", min, max);
            int sum = 0;
            for (int i = 0; i < 9; i++) sum += vals[i];
            if (sum == budget)
                return Array.ConvertAll(vals, v => (byte)v);
            Console.WriteLine($"    Total is {sum}, must be {budget}. Re-enter.");
            Console.Write("    You entered:");
            for (int i = 0; i < 9; i++) Console.Write($"  {names[i]} {vals[i]}");
            Console.WriteLine("\n    Re-enter.");
        }
    }

    // Prompt three attribute values for one category.
    static byte[] PromptThreeDots(string catLabel, string[] names,
                                  int budget, int min, int max)
    {
        Console.WriteLine($"\n  {catLabel.ToUpperInvariant()} — assign {budget} dots total (each {min}–{max}):");
        while (true)
        {
            var vals = new int[3];
            for (int i = 0; i < 3; i++)
                vals[i] = ReadInt($"    {names[i],-16}: ", min, max);
            int sum = vals[0] + vals[1] + vals[2];
            if (sum == budget)
                return new byte[] { (byte)vals[0], (byte)vals[1], (byte)vals[2] };
            Console.WriteLine($"    Total is {sum}, must be {budget}. Re-enter.");
        }
    }

    // V5 p.136 — Attribute priority.
    // All attributes start at 1 (base dot). Player distributes extra dots:
    // Primary category totals 5, Secondary 4, Tertiary 3 across 3 attributes.
    public static void Attributes(ref Character c)
    {
        Console.WriteLine("\n  ATTRIBUTES (V5 p.136)");
        Console.WriteLine("  All attributes start at 1. Distribute additional dots by priority.");
        Console.WriteLine("  Primary: 3 attributes must total 5");
        Console.WriteLine("  Secondary: total 4  |  Tertiary: total 3");

        var used = new HashSet<string>();
        var ordered = new string[3];
        string[] labels = { "Primary (total 5)", "Secondary (total 4)", "Tertiary (total 3)" };
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine();
            ordered[i] = ReadCategory($"  {labels[i]} — physical/social/mental: ", used);
            used.Add(ordered[i]);
        }

        int[] budgets = { 5, 4, 3 };

        for (int i = 0; i < 3; i++)
        {
            string cat = ordered[i];
            string[] names = cat switch
            {
                "physical" => new[] { "Strength", "Dexterity", "Stamina" },
                "social" => new[] { "Charisma", "Manipulation", "Composure" },
                _ => new[] { "Intelligence", "Wits", "Resolve" }
            };
            // min=1 because base dot is already included in the budget total
            byte[] v = PromptThreeDots(cat, names, budgets[i], 1, 5);
            switch (cat)
            {
                case "physical":
                    c.Strength = v[0]; c.Dexterity = v[1]; c.Stamina = v[2]; break;
                case "social":
                    c.Charisma = v[0]; c.Manipulation = v[1]; c.Composure = v[2]; break;
                case "mental":
                    c.Intelligence = v[0]; c.Wits = v[1]; c.Resolve = v[2]; break;
            }
        }

        Console.WriteLine("\n  Attributes assigned:");
        Console.WriteLine($"    STR {Print.Dots(c.Strength)}  DEX {Print.Dots(c.Dexterity)}  STA {Print.Dots(c.Stamina)}");
        Console.WriteLine($"    CHA {Print.Dots(c.Charisma)}  MAN {Print.Dots(c.Manipulation)}  COM {Print.Dots(c.Composure)}");
        Console.WriteLine($"    INT {Print.Dots(c.Intelligence)}  WIT {Print.Dots(c.Wits)}  RES {Print.Dots(c.Resolve)}");
    }

    // V5 p.140 — Skill priority.
    // Skills start at 0. Primary: 8 dots, Secondary: 6, Tertiary: 4.
    // Creation cap: maximum 3 dots per skill (p.142).
    public static void Skills(ref Character c)
    {
        Console.WriteLine("\n  SKILLS (V5 p.140)");
        Console.WriteLine("  Skills start at 0. Max 3 dots per skill during creation.");
        Console.WriteLine("  Primary: 8 dots  |  Secondary: 6  |  Tertiary: 4");

        var used = new HashSet<string>();
        var ordered = new string[3];
        string[] labels = { "Primary (8 dots)", "Secondary (6 dots)", "Tertiary (4 dots)" };
        for (int i = 0; i < 3; i++)
        {
            Console.WriteLine();
            ordered[i] = ReadCategory($"  {labels[i]} — physical/social/mental: ", used);
            used.Add(ordered[i]);
        }

        int[] budgets = { 8, 6, 4 };

        for (int i = 0; i < 3; i++)
        {
            string cat = ordered[i];
            string[] names = cat switch
            {
                "physical" => new[] {
                        "Athletics", "Brawl",     "Craft",
                        "Drive",     "Firearms",  "Larceny",
                        "Melee",     "Stealth",   "Survival" },
                "social" => new[] {
                        "Animal Ken",    "Etiquette",  "Insight",
                        "Intimidation",  "Leadership", "Performance",
                        "Persuasion",    "Streetwise", "Subterfuge" },
                _ => new[] {
                        "Academics", "Awareness",     "Finance",
                        "Investigation", "Medicine",  "Occult",
                        "Politics",  "Science",       "Technology" }
            };

            byte[] v = PromptNineDots(cat, names, budgets[i], 0, 3);
            switch (cat)
            {
                case "physical":
                    c.Athletics = v[0]; c.Brawl = v[1]; c.Craft = v[2];
                    c.Drive = v[3]; c.Firearms = v[4]; c.Larceny = v[5];
                    c.Melee = v[6]; c.Stealth = v[7]; c.Survival = v[8]; break;
                case "social":
                    c.AnimalKen = v[0]; c.Etiquette = v[1]; c.Insight = v[2];
                    c.Intimidation = v[3]; c.Leadership = v[4]; c.Performance = v[5];
                    c.Persuasion = v[6]; c.Streetwise = v[7]; c.Subterfuge = v[8]; break;
                case "mental":
                    c.Academics = v[0]; c.Awareness = v[1]; c.Finance = v[2];
                    c.Investigation = v[3]; c.Medicine = v[4]; c.Occult = v[5];
                    c.Politics = v[6]; c.Science = v[7]; c.Technology = v[8]; break;
            }
        }

        Console.WriteLine("\n  Skills assigned.");
    }
}