using VtmTool.Core.Enums;
using VtmTool.Core.Models;

namespace VtmTool.Core;

public static class Commands
{
    public static void NewCharacter(List<Character> characters)
    {
        Console.WriteLine("\n  === NEW CHARACTER (V5 p.136) ===");

        // Step 1 — Identity
        Console.Write("  Name: ");
        string name = Console.ReadLine()?.Trim() ?? "Unknown";

        Console.WriteLine("  Clans: Banu_Haqim, Brujah, Gangrel, Hecata, Lasombra,");
        Console.WriteLine("         Malkavian, Ministry, Nosferatu, Ravnos, Salubri,");
        Console.WriteLine("         ThinBlood, Toreador, Tremere, Tzimisce, Ventrue");
        Clan clan;
        while (true)
        {
            Console.Write("  Clan: ");
            if (Enum.TryParse<Clan>(Console.ReadLine()?.Trim(), ignoreCase: true, out clan))
                break;
            Console.WriteLine("  Invalid clan. Try again.");
        }

        // Step 2 — V5 defaults (p.136)
        var c = new Character
        {
            Name = name,
            Clan = clan,
            Generation = 13,
            BloodPotency = 1,
            Hunger = 1,
            Humanity = 7,
        };

        // Step 3 — Attributes
        Creation.Attributes(ref c);

        // Step 4 — Skills
        Creation.Skills(ref c);

        // Step 5 — Persist
        c = Db.SaveCharacter(c);
        characters.Add(c);

        Console.WriteLine($"\n  '{c.Name}' created (id {c.Id}).");
        Print.Sheet(c);
    }

    public static void ListCharacters(List<Character> characters)
    {
        if (characters.Count == 0) { Console.WriteLine("  No characters."); return; }
        Console.WriteLine();
        Console.WriteLine($"  {"ID",4}  {"Name",-20} {"Clan",-14} {"Gen",4} {"BP",3} {"Hunger",6}");
        Console.WriteLine("  " + new string('─', 56));
        foreach (var c in characters)
            Console.WriteLine($"  {c.Id,4}  {c.Name,-20} {c.Clan,-14} {c.Generation,4} {c.BloodPotency,3} {c.Hunger,6}");
        Console.WriteLine();
    }

    // =========================================================================
    // LoadLoop — inner command loop for an active character
    //
    // Takes the list + index so mutations can be written back in place and
    // persisted immediately. Pattern: copy out → mutate → write back → save.
    // =========================================================================
    public static void LoadLoop(List<Character> characters, int idx)
    {
        Character c = characters[idx];
        Console.WriteLine($"\n  Loaded '{c.Name}'. Type 'help' for commands.");

        // Write the mutated character back to the list and the DB.
        void Commit(Character updated)
        {
            c = updated;
            characters[idx] = c;
            Db.SaveCharacter(c);
        }

        var rng = new Random();

        while (true)
        {
            Console.Write($"\x1b[0;33m{c.Name}>\x1b[0m ");
            string? input = Console.ReadLine()?.Trim().ToLower();

            switch (input)
            {
                case "sheet":
                    Print.Sheet(c);
                    break;

                case "edit attr":
                    Creation.Attributes(ref c);
                    Commit(c);
                    break;

                case "edit skills":
                    Creation.Skills(ref c);
                    Commit(c);
                    break;

                case "rouse":
                    {
                        // V5 p.212 — 1d10, success on 6+.
                        int roll = rng.Next(1, 11);
                        bool success = roll >= 6;
                        Console.WriteLine($"  Rouse check — rolled {roll}: {(success ? "Success" : "Failure")}");
                        byte prev = c.Hunger;
                        c.Hunger = Rules.RouseCheck(c, roll);
                        if (c.Hunger != prev)
                            Console.WriteLine($"  Hunger {prev} → {c.Hunger}  {Print.HungerTrack(c.Hunger)}");
                        else
                            Console.WriteLine($"  Hunger unchanged ({c.Hunger})  {Print.HungerTrack(c.Hunger)}");
                        if (c.Hunger == 5)
                            Console.WriteLine("  \x1b[0;31mHunger 5 — Frenzy check required!\x1b[0m");
                        Commit(c);
                        break;
                    }

                case "damage":
                    {
                        Console.Write("  Track (health / willpower): ");
                        string? track = Console.ReadLine()?.Trim().ToLower();
                        Console.Write("  Type  (superficial / aggravated): ");
                        string? type = Console.ReadLine()?.Trim().ToLower();
                        Console.Write("  Amount: ");
                        if (!int.TryParse(Console.ReadLine()?.Trim(), out int amt) || amt <= 0)
                        { Console.WriteLine("  Invalid amount."); break; }

                        bool validTrack = track is "health" or "willpower";
                        bool validType = type is "superficial" or "aggravated";
                        if (!validTrack || !validType)
                        { Console.WriteLine("  Invalid track or type."); break; }

                        c = (track, type) switch
                        {
                            ("health", "superficial") => Rules.ApplySuperficialHealth(c, amt),
                            ("health", "aggravated") => Rules.ApplyAggravatedHealth(c, amt),
                            ("willpower", "superficial") => Rules.ApplySuperficialWillpower(c, amt),
                            ("willpower", "aggravated") => Rules.ApplyAggravatedWillpower(c, amt),
                            _ => c
                        };
                        Commit(c);
                        Console.WriteLine($"  Health:    {Print.DamageTrack(c.AggravatedHealth, c.SuperficialHealth, c.HealthMax)}  (max {c.HealthMax})");
                        Console.WriteLine($"  Willpower: {Print.DamageTrack(c.AggravatedWillpower, c.SuperficialWillpower, c.WillpowerMax)}  (max {c.WillpowerMax})");
                        int wp = Rules.WoundPenalty(c);
                        if (wp < 0) Console.WriteLine($"  \x1b[0;31mWound penalty {wp} to all dice pools\x1b[0m");
                        break;
                    }

                case "heal":
                    {
                        Console.Write("  Track (health / willpower): ");
                        string? track = Console.ReadLine()?.Trim().ToLower();
                        Console.Write("  Type  (superficial / aggravated): ");
                        string? type = Console.ReadLine()?.Trim().ToLower();
                        Console.Write("  Amount: ");
                        if (!int.TryParse(Console.ReadLine()?.Trim(), out int amt) || amt <= 0)
                        { Console.WriteLine("  Invalid amount."); break; }

                        c = (track, type) switch
                        {
                            ("health", "superficial") => Rules.HealSuperficialHealth(c, amt),
                            ("health", "aggravated") => Rules.HealAggravatedHealth(c, amt),
                            ("willpower", "superficial") => Rules.HealSuperficialWillpower(c, amt),
                            ("willpower", "aggravated") => Rules.HealAggravatedWillpower(c, amt),
                            _ => c
                        };
                        Commit(c);
                        Console.WriteLine($"  Health:    {Print.DamageTrack(c.AggravatedHealth, c.SuperficialHealth, c.HealthMax)}");
                        Console.WriteLine($"  Willpower: {Print.DamageTrack(c.AggravatedWillpower, c.SuperficialWillpower, c.WillpowerMax)}");
                        break;
                    }

                case "help":
                    Print.Help(inCharacter: true);
                    break;

                case "back":
                    return;

                default:
                    Console.WriteLine("  Unknown command. Type 'help'.");
                    break;
            }
        }
    }

    // Returns the index in characters, or -1.
    public static int FindByName(List<Character> characters, string name)
    {
        for (int i = 0; i < characters.Count; i++)
            if (string.Equals(characters[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    public static void DeleteCharacter(List<Character> characters, string name)
    {
        int idx = FindByName(characters, name);
        if (idx < 0) { Console.WriteLine($"  No character named '{name}'."); return; }
        Db.DeleteCharacter(characters[idx].Id);
        characters.RemoveAt(idx);
        Console.WriteLine($"  Deleted '{name}'.");
    }
}