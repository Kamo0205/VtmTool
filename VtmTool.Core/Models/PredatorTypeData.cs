using VtmTool.Core.Enums;

namespace VtmTool.Core.Models;

public enum PredatorType : byte
{
    None = 0,
    Alleycat = 1,
    Bagger = 2,
    BloodLeech = 3,
    Cleaner = 4,
    Consensualist = 5,
    Extortionist = 6,
    Farmer = 7,
    Graverobber = 8,
    Hitcher = 9,
    Osiris = 10,
    Sandman = 11,
    Siren = 12,
}

public struct PredatorTypeData
{
    public PredatorType Type;
    public string Description;

    // Always two skill names. If SkillBChoice is set, slot 2 is a player choice
    // between SkillBChoice[0] and SkillBChoice[1]; otherwise SkillBonuses[1] is fixed.
    public string[] SkillBonuses;      // [0] = fixed bonus A, [1] = fixed bonus B (or default)
    public string[]? SkillBChoice;      // if set: player picks slot 2 from these two options

    // Discipline bonus — one entry = no choice, two entries = player picks one
    public DisciplineName[] DisciplineChoice;

    // Specialty granted — "Skill: Label"
    public string Specialty;

    // Blood Leech only: reduces starting Humanity by 1
    public bool ReducesHumanity;
}

public static class PredatorTypes
{
    public static readonly PredatorTypeData[] All =
    {
        new PredatorTypeData
        {
            Type        = PredatorType.Alleycat,
            Description = "Hunt alone in the urban sprawl, running prey to ground.",
            SkillBonuses = new[] { "Athletics", "Brawl" },
            SkillBChoice = new[] { "Brawl", "Stealth" },  // player picks slot 2
            DisciplineChoice = new[] { DisciplineName.Celerity, DisciplineName.Potence },
            Specialty   = "Athletics: Chase",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Bagger,
            Description = "Subsist on bagged or stolen blood rather than hunting living prey.",
            SkillBonuses = new[] { "Larceny", "Streetwise" },
            DisciplineChoice = new[] { DisciplineName.Obfuscate, DisciplineName.Potence },
            Specialty   = "Larceny: Breaking and Entering",
        },
        new PredatorTypeData
        {
            Type             = PredatorType.BloodLeech,
            Description      = "Feed on other vampires — dangerous, reviled, and desperate.",
            SkillBonuses     = new[] { "Brawl", "Stealth" },
            DisciplineChoice = new[] { DisciplineName.Animalism, DisciplineName.Fortitude },
            Specialty        = "Stealth: Vampires",
            ReducesHumanity  = true,
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Cleaner,
            Description = "Feed on crime scenes, accident victims, and the recently dead.",
            SkillBonuses = new[] { "Academics", "Stealth" },
            DisciplineChoice = new[] { DisciplineName.Obfuscate, DisciplineName.Auspex },
            Specialty   = "Stealth: Covering Evidence",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Consensualist,
            Description = "Hunt only with the prey's knowledge or consent.",
            SkillBonuses = new[] { "Medicine", "Persuasion" },
            DisciplineChoice = new[] { DisciplineName.Auspex, DisciplineName.Fortitude },
            Specialty   = "Medicine: Kindred",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Extortionist,
            Description = "Use fear, blackmail, and violence to feed from those too afraid to refuse.",
            SkillBonuses = new[] { "Intimidation", "Larceny" },
            DisciplineChoice = new[] { DisciplineName.Dominate, DisciplineName.Potence },
            Specialty   = "Intimidation: Stare-Down",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Farmer,
            Description = "Feed exclusively on animals, avoiding human prey entirely.",
            SkillBonuses = new[] { "Animal Ken", "Survival" },
            DisciplineChoice = new[] { DisciplineName.Animalism, DisciplineName.Protean },
            Specialty   = "Animal Ken: Specific Animal",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Graverobber,
            Description = "Feed from the blood of fresh corpses.",
            SkillBonuses = new[] { "Occult", "Medicine" },
            DisciplineChoice = new[] { DisciplineName.Fortitude, DisciplineName.Oblivion },
            Specialty   = "Occult: Grave Rituals",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Hitcher,
            Description = "Hunt hitchhikers, travellers, and those who accept rides from strangers.",
            SkillBonuses = new[] { "Insight", "Persuasion" },
            DisciplineChoice = new[] { DisciplineName.Auspex, DisciplineName.Dominate },
            Specialty   = "Insight: Lies",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Osiris,
            Description = "Build a cult or congregation that worships you and feeds you willingly.",
            SkillBonuses = new[] { "Occult", "Performance" },
            DisciplineChoice = new[] { DisciplineName.BloodSorcery, DisciplineName.Presence },
            Specialty   = "Occult: Specific Religion",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Sandman,
            Description = "Creep into homes at night and feed from sleeping victims.",
            SkillBonuses = new[] { "Stealth", "Medicine" },
            DisciplineChoice = new[] { DisciplineName.Auspex, DisciplineName.Dominate },
            Specialty   = "Medicine: Anesthetics",
        },
        new PredatorTypeData
        {
            Type        = PredatorType.Siren,
            Description = "Use seduction and charm to lure prey into willing surrender.",
            SkillBonuses = new[] { "Persuasion", "Subterfuge" },
            DisciplineChoice = new[] { DisciplineName.Fortitude, DisciplineName.Presence },
            Specialty   = "Subterfuge: Seduction",
        },
    };

    public static PredatorTypeData? Get(PredatorType type)
    {
        foreach (var d in All)
            if (d.Type == type) return d;
        return null;
    }

    // Display name with spaces
    public static string DisplayName(PredatorType t) => t switch
    {
        PredatorType.BloodLeech => "Blood Leech",
        PredatorType.Consensualist => "Consensualist",
        PredatorType.Extortionist => "Extortionist",
        PredatorType.Graverobber => "Graverobber",
        _ => t.ToString(),
    };
}