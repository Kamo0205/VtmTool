using Microsoft.Data.Sqlite;
using System.Text;
using static VtmTool.App.Program;

namespace VtmTool.App;

public class Program
{
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

        // Rouse check: roll 1d10, on a failure (1–5) Hunger rises by 1.
        // Returns the new Hunger value. Hunger cannot exceed 5.
        public static byte RouseCheck(in Character c, Random rng)
        {
            bool success = rng.Next(1, 11) >= 6;
            if (success) return c.Hunger;
            return (byte)Math.Min(c.Hunger + 1, 5);
        }
    }

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
                // INSERT — let SQLite assign the rowid
                cmd.CommandText = @"
                    INSERT INTO character (
                        name, clan, generation, blood_potency,
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
                        $name, $clan, $generation, $blood_potency,
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
                // UPDATE — row already exists
                cmd.CommandText = @"
                    UPDATE character SET
                        name                  = $name,
                        clan                  = $clan,
                        generation            = $generation,
                        blood_potency         = $blood_potency,
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

        public static Character? LoadCharacter(int id)
        {
            return null;
        }

        public static List<(int id, string name, string clan, int generation)> ListCharacters()
        {
            return new List<(int id, string name, string clan, int generation)>(0);
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
            var c = new Character { Generation = 13, Hunger = 1 };

            Console.Write("Name: ");
            c.Name = Console.ReadLine()?.Trim() ?? "Unknown";

            Console.Write("Clan: ");
            if (Enum.TryParse<Clan>(Console.ReadLine()?.Trim(), ignoreCase: true, out var clan))
                c.Clan = clan;

            c = Db.SaveCharacter(c);      // Id is filled in here after INSERT
            characters.Add(c);
            Console.WriteLine($"Created '{c.Name}' (id {c.Id}).");
        }

        public static void ListCharacters(List<Character> characters)
        {
            if (characters.Count == 0) { Console.WriteLine("No characters."); return; }
            Console.WriteLine($"{"Name",-20} {"Clan",-14} {"Gen",4} {"BP",4} {"Hunger",6}");
            Console.WriteLine(new string('-', 52));
            foreach (var c in characters)
                Console.WriteLine($"{c.Name,-20} {c.Clan,-14} {c.Generation,4} {c.BloodPotency,4} {c.Hunger,6}");
        }

        public static void LoadCharacter(List<Character> characters, string name)
        {
            var c = characters.Find(x =>
                string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

            if (c.Id == 0) { Console.WriteLine($"No character named '{name}'."); return; }

            Console.WriteLine();
            Console.WriteLine($"  Name       : {c.Name}");
            Console.WriteLine($"  Clan       : {c.Clan}");
            Console.WriteLine($"  Generation : {c.Generation}");
            Console.WriteLine($"  Blood Pot. : {c.BloodPotency} / {Rules.MaxBloodPotency(c.Generation)}");
            Console.WriteLine($"  Hunger     : {c.Hunger}");
            Console.WriteLine($"  Health     : {c.AggravatedHealth} Agg / {c.SuperficialHealth} Sup / {c.HealthMax} Max");
            Console.WriteLine($"  Willpower  : {c.AggravatedWillpower} Agg / {c.SuperficialWillpower} Sup / {c.WillpowerMax} Max");
            Console.WriteLine();
            Console.WriteLine("  -- Attributes --");
            Console.WriteLine($"  STR {c.Strength}  DEX {c.Dexterity}  STA {c.Stamina}");
            Console.WriteLine($"  CHA {c.Charisma}  MAN {c.Manipulation}  COM {c.Composure}");
            Console.WriteLine($"  INT {c.Intelligence}  WIT {c.Wits}  RES {c.Resolve}");
        }
    }

    static List<Character> _characters = new();

    public static void Main(string[] args)
    {
        Db.Init();
        _characters = Db.LoadAll();
        while (true)
        {
            Console.Write("\x1b[0;33m>\x1b[0m ");
            var input = Console.ReadLine()?.Trim().ToLower();

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
                    var name = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        Commands.LoadCharacter(_characters, name);
                    else
                        Console.WriteLine("No name entered.");
                    break;
                case "quit":
                    Console.WriteLine("Exiting...");
                    return;
                default:
                    Console.WriteLine("Commands: new | list | load | quit");
                    break;
            }
        }
    }
}
