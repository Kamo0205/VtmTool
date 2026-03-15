using System.Text;
using static VtmTool.App.Program;

namespace VtmTool.App;

public class Program
{
    public enum Clan
    {
        Banu_Haqim, Brujah, Gangrel, Hecata, Lasombra,
        Malkavian, Ministry, Nosferatu, Ravnos, Salubri,
        ThinBlood, Toreador, Tremere, Tzimisce, Ventrue
    }

    public struct Character
    {
        public int Id;
        public string Name;
        public Clan Clan;
        public byte Generation; // 4-16
        public byte BloodPotency; // 0-10, capped by Generation

        // Attributes
        public byte Strength, Dexterity, Stamina;
        public byte Charisma, Manipulation, Composure;
        public byte Intelligence, Wits, Resolve;

        //Skills
        public byte Athletics, Brawl, Craft, Drive, Firearms,
               Larceny, Melee, Stealth, Survival;
        public byte AnimalKen, Etiquette, Insight, Intimidation,
                   Leadership, Performance, Persuasion, Streetwise, Subterfuge;
        public byte Academics, Awareness, Finance, Investigation,
                   Medicine, Occult, Politics, Science, Technology;

        // Health & Willpower
        public uint HealthMax => Stamina + 3;       // = Stamina + 3
        public uint WillpowerMax => Composure + Resolve;    // = Composure + Resolve
        public uint AggravatedHealth;
        public uint SuperficialHealth;
        public uint AggravatedWillpower;
        public uint SuperficialWillpower;

        // Hunger
        public uint Hunger; // 0-5; never stored float
    }

    public static class Rules
    {
        // Generation caps Blood Potency maximum (V5 corebook p.216)
        public static int MaxBloodPotency(int generation) => generation switch
        {
            <= 8 => 10,
            9 => 4,
            10 => 3,
            11 => 2,
            >= 12 => 1,
            //_ => 0
        };

        // Wound penalty to dice pools (V5 p.127)
        public static uint WoundPenalty(Character c) { return default; }

        // Rouse check result affects Hunger
        public static uint RouseCheck(Character c, Random rng) { return default; }
    }

    public static class Db
    {
        const string ConnectionString = "Data Source=vtm.db";

        public static void Init()  // called once at startup
        {

        }
        public static void SaveCharacter(Character c)
        {

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

        }
    }

    public readonly struct Commands
    {
        private static List<Character> Characters = new List<Character>();
        private static int CharacterCount = 0;

        public static void NewCharacter()
        {
            Character character = new Character();

            Characters[CharacterCount++] = character;
            character.Id = CharacterCount - 1;

            Console.Write("Enter name: ");
            if (Console.ReadLine() is string name && string.IsNullOrWhiteSpace(name))
                character.Name = name;

            Db.SaveCharacter(character);
        }

        public static void ListCharacters()
        {
            StringBuilder output = new StringBuilder(Characters.Count + 1);
            output.Append("Character\t|\tClan\t|\tGeneration");

            for (int i = 0; i < Characters.Count; i++)
            {
                var clan = Characters[i].Clan;
                output.Append($"{Characters[i].Name}\t|\t{nameof(clan)}\t|\t{Characters[i].Generation}");
            }
            // Alternate using the DB
            //foreach (var character in Db.ListCharacters())
            //{
            //    output.Append($"{character.name}\t|\t{character.clan}\t|\t{character.generation}");
            //}
            Console.WriteLine(output.ToString());
        }

        public static void LoadCharacter(string name)
        {
            StringBuilder output = new StringBuilder(Characters.Count + 1);
            output.Append("Character\t|\tClan\t|\tGeneration");
            int characterIndex = Characters.SingleOrDefault(c => c.Name == name).Id;
            var clan = Characters[characterIndex].Clan;
            output.Append($"{Characters[characterIndex].Name}\t|\t{nameof(clan)}\t|\t{Characters[characterIndex].Generation}");
            Console.WriteLine(output.ToString());
        }
    }

    public static void Main(string[] args)
    {
        Db.Init();
        while (true)
        {
            Console.Write("\x1b[0;33m>\x1b[0m ");
            var input = Console.ReadLine()?.Trim().ToLower();
            int retryCount = 3;
            switch (input)
            {
                case "new":
                    Console.WriteLine("Creating new character...");
                    Commands.NewCharacter();
                    break;
                case "list":
                    Console.WriteLine("List characters...");
                    Commands.ListCharacters();
                    break;
                case "load":
                    Console.WriteLine("Load character...");
                    Console.Write("Character name: ");
                    do
                    {
                        if (retryCount <= 3)
                        {
                            Console.WriteLine("Failed to read charactrer name in...");
                            break;
                        }
                        retryCount++;
                        if (Console.ReadLine() is string name && string.IsNullOrWhiteSpace(name))
                        {
                            Commands.LoadCharacter(name);
                            break;
                        }
                        else continue;
                    }
                    while (true);
                    break;
                case "quit":
                    Console.WriteLine("Exiting...");
                    return;
                default:
                    Console.WriteLine("=== Commands ===");
                    Console.WriteLine("new - Create a new character");
                    Console.WriteLine("list - List characters");
                    Console.WriteLine("load - Load a character");
                    Console.WriteLine("help - Print available commands");
                    Console.WriteLine("quit - Exit program");
                    break;
            }
        }
    }
}
