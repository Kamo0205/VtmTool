using Microsoft.Data.Sqlite;
using System.Text;
using static VtmTool.App.Program;

namespace VtmTool.App;

    /// <summary>
    /// Specifies the available vampire clans.
    /// </summary>
    /// <remarks>Each value represents a distinct clan, typically used to categorize characters or entities by
    /// their lineage and abilities within the context of the application. The enumeration includes both major and minor
    /// clans as commonly recognized in vampire-themed settings.</remarks>
    public enum Clan
    {
        Banu_Haqim, Brujah, Gangrel, Hecata, Lasombra,
        Malkavian, Ministry, Nosferatu, Ravnos, Salubri,
        ThinBlood, Toreador, Tremere, Tzimisce, Ventrue
    }

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

    public static class Rules
    {
        // Generation caps Blood Potency maximum (V5 corebook p.216)
        public static byte MaxBloodPotency(byte generation) => generation switch
        {
            <= 8 => 10,
            9 => 4,
            10 => 3,
            11 => 2,
            _ => 1   // 12–16 (thin-bloods etc.)
        };

        // Wound penalty to dice pools (V5 p.127)
        // Returns number of dice to subtract from pools.
        public static int WoundPenalty(in Character c)
        {
            int total = c.AggravatedHealth + c.SuperficialHealth;
            int max = c.HealthMax;
            if (total == 0) return 0;
            if (total >= max) return -3;
            if (total >= max - 1) return -2;
            if (total >= max - 2) return -1;
            return 0;
        }

        // V5 p.212 — Rouse check: roll 1d10, fail on 1–5.
        // Failure raises Hunger by 1. Hunger cannot exceed 5.
        // Returns updated Hunger value; caller writes it back.
        public static byte RouseCheck(in Character c, Random rng)
        {
            bool success = rng.Next(1, 11) >= 6;
            if (success) return c.Hunger;
            return (byte)Math.Min(c.Hunger + 1, 5);
        }

        // V5 p.127 — Superficial health damage.
        // Fills available boxes; overflow converts to Aggravated.
        public static Character ApplySuperficialHealth(Character c, int amount)
        {
            int remaining = c.HealthMax - c.AggravatedHealth - c.SuperficialHealth;
            int direct = Math.Min(amount, remaining);
            int overflow = amount - direct;
            c.SuperficialHealth = (byte)Math.Min(c.SuperficialHealth + direct, c.HealthMax);
            if (overflow > 0)
                c = ApplyAggravatedHealth(c, overflow);
            return c;
    }

        // V5 p.127 — Aggravated health damage displaces Superficial if needed.
        public static Character ApplyAggravatedHealth(Character c, int amount)
        {
            c.AggravatedHealth = (byte)Math.Min(c.AggravatedHealth + amount, c.HealthMax);
            int total = c.AggravatedHealth + c.SuperficialHealth;
            if (total > c.HealthMax)
                c.SuperficialHealth = (byte)(c.HealthMax - c.AggravatedHealth);
            return c;
        }

        // Recover superficial health damage.
        public static Character HealSuperficialHealth(Character c, int amount)
        {
            c.SuperficialHealth = (byte)Math.Max(c.SuperficialHealth - amount, 0);
            return c;
        }

        // V5 p.127 — Superficial willpower damage; overflow becomes Aggravated.
        public static Character ApplySuperficialWillpower(Character c, int amount)
        {
            int remaining = c.WillpowerMax - c.AggravatedWillpower - c.SuperficialWillpower;
            int direct = Math.Min(amount, remaining);
            int overflow = amount - direct;
            c.SuperficialWillpower = (byte)Math.Min(c.SuperficialWillpower + direct, c.WillpowerMax);
            if (overflow > 0)
                c.AggravatedWillpower = (byte)Math.Min(c.AggravatedWillpower + overflow, c.WillpowerMax);
            return c;
        }

        // Recover superficial willpower damage.
        public static Character HealSuperficialWillpower(Character c, int amount)
        {
            c.SuperficialWillpower = (byte)Math.Max(c.SuperficialWillpower - amount, 0);
            return c;
        }
    }

    // =========================================================================
    // Db
    //
    // All SQLite access. Raw SQL, no ORM. Owns no state.
    //
    //   Init()    — create table if absent, once at startup
    //   LoadAll() — called once at startup, returns everything
    //   Save()    — insert (Id==0) or update (Id>0), after every mutation
    //   Delete()  — remove by Id
    // =========================================================================
    /// <summary>
    /// Provides static methods for initializing, loading, saving, listing, and deleting character data in the local
    /// SQLite database.
    /// </summary>
    /// <remarks>The Db class manages the persistence of character records using a SQLite database file named
    /// "vtm.db". It is intended to be used as a central data access layer for character-related operations. All methods
    /// are thread-unsafe and should be called from a single thread or synchronized externally if used
    /// concurrently.</remarks>
    public static class Db
    {
        const string ConnectionString = "Data Source=vtm.db";

        // Schema: one row per character, columns 1:1 with struct fields.
        // 'id' is INTEGER PRIMARY KEY which in SQLite is the rowid alias —
        // it auto-increments on INSERT when omitted or passed as 0.
        const string CreateTableSql = @"
            CREATE TABLE IF NOT EXISTS character (
                id                    INTEGER PRIMARY KEY,
                name                  TEXT    NOT NULL,
                clan                  INTEGER NOT NULL,
                generation            INTEGER NOT NULL,
                blood_potency         INTEGER NOT NULL,
                humanity              INTEGER NOT NULL,
                strength              INTEGER NOT NULL,
                dexterity             INTEGER NOT NULL,
                stamina               INTEGER NOT NULL,
                charisma              INTEGER NOT NULL,
                manipulation          INTEGER NOT NULL,
                composure             INTEGER NOT NULL,
                intelligence          INTEGER NOT NULL,
                wits                  INTEGER NOT NULL,
                resolve               INTEGER NOT NULL,
                athletics             INTEGER NOT NULL,
                brawl                 INTEGER NOT NULL,
                craft                 INTEGER NOT NULL,
                drive                 INTEGER NOT NULL,
                firearms              INTEGER NOT NULL,
                larceny               INTEGER NOT NULL,
                melee                 INTEGER NOT NULL,
                stealth               INTEGER NOT NULL,
                survival              INTEGER NOT NULL,
                animal_ken            INTEGER NOT NULL,
                etiquette             INTEGER NOT NULL,
                insight               INTEGER NOT NULL,
                intimidation          INTEGER NOT NULL,
                leadership            INTEGER NOT NULL,
                performance           INTEGER NOT NULL,
                persuasion            INTEGER NOT NULL,
                streetwise            INTEGER NOT NULL,
                subterfuge            INTEGER NOT NULL,
                academics             INTEGER NOT NULL,
                awareness             INTEGER NOT NULL,
                finance               INTEGER NOT NULL,
                investigation         INTEGER NOT NULL,
                medicine              INTEGER NOT NULL,
                occult                INTEGER NOT NULL,
                politics              INTEGER NOT NULL,
                science               INTEGER NOT NULL,
                technology            INTEGER NOT NULL,
                aggravated_health     INTEGER NOT NULL,
                superficial_health    INTEGER NOT NULL,
                aggravated_willpower  INTEGER NOT NULL,
                superficial_willpower INTEGER NOT NULL,
                hunger                INTEGER NOT NULL
            );";

        // Called once at startup. Creates the DB file and table if absent.
        public static void Init()
        {
            //if (!File.Exists("vtm.db"))
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = CreateTableSql;
            cmd.ExecuteNonQuery();
        }

        // Load every character from the DB. Called once at startup.
        // Returns a List because the count is DB-driven and unknown at compile time.
        public static List<Character> LoadAll()
        {
            var list = new List<Character>();

            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM character ORDER BY id;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(ReadCharacter(reader));

            return list;
        }

        // Insert or update. If c.Id == 0 the row is new (SQLite assigns the id);
        // the returned Character has Id filled in. If c.Id > 0 the row is updated.
        public static Character SaveCharacter(Character c)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();

            if (c.Id == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO character (
                        name, clan, generation, blood_potency, humanity,
                        strength, dexterity, stamina,
                        charisma, manipulation, composure,
                        intelligence, wits, resolve,
                        athletics, brawl, craft, drive, firearms,
                        larceny, melee, stealth, survival,
                        animal_ken, etiquette, insight, intimidation,
                        leadership, performance, persuasion, streetwise, subterfuge,
                        academics, awareness, finance, investigation,
                        medicine, occult, politics, science, technology,
                        aggravated_health, superficial_health,
                        aggravated_willpower, superficial_willpower,
                        hunger
                    ) VALUES (
                        $name, $clan, $generation, $blood_potency, $humanity,
                        $strength, $dexterity, $stamina,
                        $charisma, $manipulation, $composure,
                        $intelligence, $wits, $resolve,
                        $athletics, $brawl, $craft, $drive, $firearms,
                        $larceny, $melee, $stealth, $survival,
                        $animal_ken, $etiquette, $insight, $intimidation,
                        $leadership, $performance, $persuasion, $streetwise, $subterfuge,
                        $academics, $awareness, $finance, $investigation,
                        $medicine, $occult, $politics, $science, $technology,
                        $aggravated_health, $superficial_health,
                        $aggravated_willpower, $superficial_willpower,
                        $hunger
                    );
                    SELECT last_insert_rowid();";
                BindParams(cmd, c);
                c.Id = (int)(long)cmd.ExecuteScalar()!;
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE character SET
                        name                  = $name,
                        clan                  = $clan,
                        generation            = $generation,
                        blood_potency         = $blood_potency,
                        humanity              = $humanity,
                        strength              = $strength,
                        dexterity             = $dexterity,
                        stamina               = $stamina,
                        charisma              = $charisma,
                        manipulation          = $manipulation,
                        composure             = $composure,
                        intelligence          = $intelligence,
                        wits                  = $wits,
                        resolve               = $resolve,
                        athletics             = $athletics,
                        brawl                 = $brawl,
                        craft                 = $craft,
                        drive                 = $drive,
                        firearms              = $firearms,
                        larceny               = $larceny,
                        melee                 = $melee,
                        stealth               = $stealth,
                        survival              = $survival,
                        animal_ken            = $animal_ken,
                        etiquette             = $etiquette,
                        insight               = $insight,
                        intimidation          = $intimidation,
                        leadership            = $leadership,
                        performance           = $performance,
                        persuasion            = $persuasion,
                        streetwise            = $streetwise,
                        subterfuge            = $subterfuge,
                        academics             = $academics,
                        awareness             = $awareness,
                        finance               = $finance,
                        investigation         = $investigation,
                        medicine              = $medicine,
                        occult                = $occult,
                        politics              = $politics,
                        science               = $science,
                        technology            = $technology,
                        aggravated_health     = $aggravated_health,
                        superficial_health    = $superficial_health,
                        aggravated_willpower  = $aggravated_willpower,
                        superficial_willpower = $superficial_willpower,
                        hunger                = $hunger
                    WHERE id = $id;";
                BindParams(cmd, c);
                cmd.Parameters.AddWithValue("$id", c.Id);
                cmd.ExecuteNonQuery();
            }
            return c;
        }

        public static void DeleteCharacter(int id)
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM character WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        // -- Private helpers --------------------------------------------------

        // Bind all non-id parameters. Used by both INSERT and UPDATE paths.
        // One place to update if the struct gains new fields.
        static void BindParams(SqliteCommand cmd, Character c)
        {
            cmd.Parameters.AddWithValue("$name", c.Name);
            cmd.Parameters.AddWithValue("$clan", (byte)c.Clan);
            cmd.Parameters.AddWithValue("$generation", c.Generation);
            cmd.Parameters.AddWithValue("$blood_potency", c.BloodPotency);
            cmd.Parameters.AddWithValue("$humanity", c.Humanity);
            cmd.Parameters.AddWithValue("$strength", c.Strength);
            cmd.Parameters.AddWithValue("$dexterity", c.Dexterity);
            cmd.Parameters.AddWithValue("$stamina", c.Stamina);
            cmd.Parameters.AddWithValue("$charisma", c.Charisma);
            cmd.Parameters.AddWithValue("$manipulation", c.Manipulation);
            cmd.Parameters.AddWithValue("$composure", c.Composure);
            cmd.Parameters.AddWithValue("$intelligence", c.Intelligence);
            cmd.Parameters.AddWithValue("$wits", c.Wits);
            cmd.Parameters.AddWithValue("$resolve", c.Resolve);
            cmd.Parameters.AddWithValue("$athletics", c.Athletics);
            cmd.Parameters.AddWithValue("$brawl", c.Brawl);
            cmd.Parameters.AddWithValue("$craft", c.Craft);
            cmd.Parameters.AddWithValue("$drive", c.Drive);
            cmd.Parameters.AddWithValue("$firearms", c.Firearms);
            cmd.Parameters.AddWithValue("$larceny", c.Larceny);
            cmd.Parameters.AddWithValue("$melee", c.Melee);
            cmd.Parameters.AddWithValue("$stealth", c.Stealth);
            cmd.Parameters.AddWithValue("$survival", c.Survival);
            cmd.Parameters.AddWithValue("$animal_ken", c.AnimalKen);
            cmd.Parameters.AddWithValue("$etiquette", c.Etiquette);
            cmd.Parameters.AddWithValue("$insight", c.Insight);
            cmd.Parameters.AddWithValue("$intimidation", c.Intimidation);
            cmd.Parameters.AddWithValue("$leadership", c.Leadership);
            cmd.Parameters.AddWithValue("$performance", c.Performance);
            cmd.Parameters.AddWithValue("$persuasion", c.Persuasion);
            cmd.Parameters.AddWithValue("$streetwise", c.Streetwise);
            cmd.Parameters.AddWithValue("$subterfuge", c.Subterfuge);
            cmd.Parameters.AddWithValue("$academics", c.Academics);
            cmd.Parameters.AddWithValue("$awareness", c.Awareness);
            cmd.Parameters.AddWithValue("$finance", c.Finance);
            cmd.Parameters.AddWithValue("$investigation", c.Investigation);
            cmd.Parameters.AddWithValue("$medicine", c.Medicine);
            cmd.Parameters.AddWithValue("$occult", c.Occult);
            cmd.Parameters.AddWithValue("$politics", c.Politics);
            cmd.Parameters.AddWithValue("$science", c.Science);
            cmd.Parameters.AddWithValue("$technology", c.Technology);
            cmd.Parameters.AddWithValue("$aggravated_health", c.AggravatedHealth);
            cmd.Parameters.AddWithValue("$superficial_health", c.SuperficialHealth);
            cmd.Parameters.AddWithValue("$aggravated_willpower", c.AggravatedWillpower);
            cmd.Parameters.AddWithValue("$superficial_willpower", c.SuperficialWillpower);
            cmd.Parameters.AddWithValue("$hunger", c.Hunger);
        }

        // Read one character row. Column order matches SELECT *.
        static Character ReadCharacter(SqliteDataReader r) => new Character
        {
            Id = r.GetInt32(r.GetOrdinal("id")),
            Name = r.GetString(r.GetOrdinal("name")),
            Clan = (Clan)r.GetByte(r.GetOrdinal("clan")),
            Generation = r.GetByte(r.GetOrdinal("generation")),
            BloodPotency = r.GetByte(r.GetOrdinal("blood_potency")),
            Humanity = r.GetByte(r.GetOrdinal("humanity")),
            Strength = r.GetByte(r.GetOrdinal("strength")),
            Dexterity = r.GetByte(r.GetOrdinal("dexterity")),
            Stamina = r.GetByte(r.GetOrdinal("stamina")),
            Charisma = r.GetByte(r.GetOrdinal("charisma")),
            Manipulation = r.GetByte(r.GetOrdinal("manipulation")),
            Composure = r.GetByte(r.GetOrdinal("composure")),
            Intelligence = r.GetByte(r.GetOrdinal("intelligence")),
            Wits = r.GetByte(r.GetOrdinal("wits")),
            Resolve = r.GetByte(r.GetOrdinal("resolve")),
            Athletics = r.GetByte(r.GetOrdinal("athletics")),
            Brawl = r.GetByte(r.GetOrdinal("brawl")),
            Craft = r.GetByte(r.GetOrdinal("craft")),
            Drive = r.GetByte(r.GetOrdinal("drive")),
            Firearms = r.GetByte(r.GetOrdinal("firearms")),
            Larceny = r.GetByte(r.GetOrdinal("larceny")),
            Melee = r.GetByte(r.GetOrdinal("melee")),
            Stealth = r.GetByte(r.GetOrdinal("stealth")),
            Survival = r.GetByte(r.GetOrdinal("survival")),
            AnimalKen = r.GetByte(r.GetOrdinal("animal_ken")),
            Etiquette = r.GetByte(r.GetOrdinal("etiquette")),
            Insight = r.GetByte(r.GetOrdinal("insight")),
            Intimidation = r.GetByte(r.GetOrdinal("intimidation")),
            Leadership = r.GetByte(r.GetOrdinal("leadership")),
            Performance = r.GetByte(r.GetOrdinal("performance")),
            Persuasion = r.GetByte(r.GetOrdinal("persuasion")),
            Streetwise = r.GetByte(r.GetOrdinal("streetwise")),
            Subterfuge = r.GetByte(r.GetOrdinal("subterfuge")),
            Academics = r.GetByte(r.GetOrdinal("academics")),
            Awareness = r.GetByte(r.GetOrdinal("awareness")),
            Finance = r.GetByte(r.GetOrdinal("finance")),
            Investigation = r.GetByte(r.GetOrdinal("investigation")),
            Medicine = r.GetByte(r.GetOrdinal("medicine")),
            Occult = r.GetByte(r.GetOrdinal("occult")),
            Politics = r.GetByte(r.GetOrdinal("politics")),
            Science = r.GetByte(r.GetOrdinal("science")),
            Technology = r.GetByte(r.GetOrdinal("technology")),
            AggravatedHealth = r.GetByte(r.GetOrdinal("aggravated_health")),
            SuperficialHealth = r.GetByte(r.GetOrdinal("superficial_health")),
            AggravatedWillpower = r.GetByte(r.GetOrdinal("aggravated_willpower")),
            SuperficialWillpower = r.GetByte(r.GetOrdinal("superficial_willpower")),
            Hunger = r.GetByte(r.GetOrdinal("hunger")),
        };
    }

    public readonly struct Commands
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
        Console.Write("  Clan: ");
        Enum.TryParse<Clan>(Console.ReadLine()?.Trim(), ignoreCase: true, out Clan clan);

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
    static void LoadLoop(List<Character> characters, int idx)
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
                        c.Hunger = Rules.RouseCheck(c, rng);
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
            
                        c = (track, type) switch
                        {
                            ("health", "superficial") => Rules.ApplySuperficialHealth(c, amt),
                            ("health", "aggravated") => Rules.ApplyAggravatedHealth(c, amt),
                            ("willpower", "superficial") => Rules.ApplySuperficialWillpower(c, amt),
                            ("willpower", "aggravated") => Rules.ApplySuperficialWillpower(c, amt), // placeholder
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
                        Console.Write("  Amount: ");
                        if (!int.TryParse(Console.ReadLine()?.Trim(), out int amt) || amt <= 0)
                        { Console.WriteLine("  Invalid amount."); break; }

                        c = track switch
                        {
                            "health" => Rules.HealSuperficialHealth(c, amt),
                            "willpower" => Rules.HealSuperficialWillpower(c, amt),
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

        // Full V5 character sheet.
        public static void Sheet(in Character c)
        {
            Console.WriteLine();
            Console.WriteLine($"  \x1b[1m{c.Name}\x1b[0m");
            Console.WriteLine($"  {c.Clan}  ·  {c.Generation}th Generation  ·  Humanity {Dots(c.Humanity, 10)}");
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

public class Program
{
    static List<Character> _characters = new();

    // =========================================================================
    // LoadLoop — inner command loop for an active character
    //
    // Takes the list + index so mutations can be written back in place and
    // persisted immediately. Pattern: copy out → mutate → write back → save.
    // =========================================================================
    static void LoadLoop(List<Character> characters, int idx)
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
                        // V5 p.212 — 1d10, success on 6+.
                        int roll = rng.Next(1, 11);
                        bool success = roll >= 6;
                        Console.WriteLine($"  Rouse check — rolled {roll}: {(success ? "Success" : "Failure")}");
                        byte prev = c.Hunger;
                        c.Hunger = Rules.RouseCheck(c, rng);
                        if (c.Hunger != prev)
                            Console.WriteLine($"  Hunger {prev} → {c.Hunger}  {Print.HungerTrack(c.Hunger)}");
                        else
                            Console.WriteLine($"  Hunger unchanged ({c.Hunger})  {Print.HungerTrack(c.Hunger)}");
                        if (c.Hunger == 5)
                            Console.WriteLine("  \x1b[0;31mHunger 5 — Frenzy check required!\x1b[0m");
                        Commit(c);
                    break;
                case "damage":
                    Console.Write("  Track (health / willpower): ");
                    string? track = Console.ReadLine()?.Trim().ToLower();
                    Console.Write("  Type  (superficial / aggravated): ");
                    string? type = Console.ReadLine()?.Trim().ToLower();
                    Console.Write("  Amount: ");
                    if (!int.TryParse(Console.ReadLine()?.Trim(), out int amt) || amt <= 0)
                    { Console.WriteLine("  Invalid amount."); break; }

                    c = (track, type) switch
                    {
                        ("health", "superficial") => Rules.ApplySuperficialHealth(c, amt),
                        ("health", "aggravated") => Rules.ApplyAggravatedHealth(c, amt),
                        ("willpower", "superficial") => Rules.ApplySuperficialWillpower(c, amt),
                        ("willpower", "aggravated") => Rules.ApplySuperficialWillpower(c, amt), // placeholder
                        _ => c
                    };
                    Commit(c);
                    Console.WriteLine($"  Health:    {Print.DamageTrack(c.AggravatedHealth, c.SuperficialHealth, c.HealthMax)}  (max {c.HealthMax})");
                    Console.WriteLine($"  Willpower: {Print.DamageTrack(c.AggravatedWillpower, c.SuperficialWillpower, c.WillpowerMax)}  (max {c.WillpowerMax})");
                    int wp = Rules.WoundPenalty(c);
                    if (wp < 0) Console.WriteLine($"  \x1b[0;31mWound penalty {wp} to all dice pools\x1b[0m");
                    break;
                case "heal":
                    break;
                case "back":
                    return;
                default:
                    Console.WriteLine("  Unknown command. Type 'help'.");
                    break;
            }
        }
    }

    public static void Main(string[] args)
    {
        Db.Init();
        _characters = Db.LoadAll();

        Console.WriteLine("Vampire: The Masquerade V5 — Character Tool");
        Console.WriteLine($"  {_characters.Count} character(s) loaded.");
        Console.WriteLine("  Type 'help' for commands.");

        while (true)
        {
            Console.Write("\x1b[0;33m>\x1b[0m ");
            var input = Console.ReadLine()?.Trim().ToLower();
            string? name = string.Empty;

            switch (input)
            {
                case "new":
                    Commands.NewCharacter(_characters);
                    break;
                case "list":
                    Commands.ListCharacters(_characters);
                    break;
                case "load":
                    Console.Write("Character name: ");
                    name = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) { Console.WriteLine("  No name entered."); break; }
                    int idx = Commands.FindByName(_characters, name);
                    if (idx < 0) { Console.WriteLine($"  No character named '{name}'."); break; }
                    LoadLoop(_characters, idx);
                    break;
                case "delete":
                    Console.Write("  Character name: ");
                    name = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        Commands.DeleteCharacter(_characters, name);
                    break;
                case "help":
                    Print.Help(inCharacter: false);
                    break;
                case "quit":
                    Console.WriteLine("  Exiting.");
                    return;
                default:
                    Console.WriteLine("  Unknown command. Type 'help'.");
                    break;
            }
        }
    }
}
