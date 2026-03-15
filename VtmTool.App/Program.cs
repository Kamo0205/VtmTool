using VtmTool.Core;
using VtmTool.Core.Models;

namespace VtmTool.App;

public class Program
{
    static List<Character> _characters = new();

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
                    Commands.LoadLoop(_characters, idx);
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
