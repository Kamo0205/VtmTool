using System;
using VtmTool.Core.Enums;

namespace VtmTool.Core.Models;

public static class ClanDisciplines
{
    public static DisciplineName[] For(Clan clan) => clan switch
    {
        Clan.Banu_Haqim => new[] { DisciplineName.BloodSorcery, DisciplineName.Celerity, DisciplineName.Obfuscate },
        Clan.Brujah => new[] { DisciplineName.Celerity, DisciplineName.Potence, DisciplineName.Presence },
        Clan.Gangrel => new[] { DisciplineName.Animalism, DisciplineName.Fortitude, DisciplineName.Protean },
        Clan.Hecata => new[] { DisciplineName.Auspex, DisciplineName.Fortitude, DisciplineName.Oblivion },
        Clan.Lasombra => new[] { DisciplineName.Dominate, DisciplineName.Oblivion, DisciplineName.Potence },
        Clan.Malkavian => new[] { DisciplineName.Auspex, DisciplineName.Dominate, DisciplineName.Obfuscate },
        Clan.Ministry => new[] { DisciplineName.Obfuscate, DisciplineName.Presence, DisciplineName.Protean },
        Clan.Nosferatu => new[] { DisciplineName.Animalism, DisciplineName.Obfuscate, DisciplineName.Potence },
        Clan.Ravnos => new[] { DisciplineName.Animalism, DisciplineName.Chimerstry, DisciplineName.Presence },
        Clan.Salubri => new[] { DisciplineName.Auspex, DisciplineName.Fortitude, DisciplineName.Oblivion },
        Clan.ThinBlood => new[] { DisciplineName.ThinBloodAlchemy, DisciplineName.Auspex, DisciplineName.Dominate },
        Clan.Toreador => new[] { DisciplineName.Auspex, DisciplineName.Celerity, DisciplineName.Presence },
        Clan.Tremere => new[] { DisciplineName.Auspex, DisciplineName.BloodSorcery, DisciplineName.Dominate },
        Clan.Tzimisce => new[] { DisciplineName.Animalism, DisciplineName.Dominate, DisciplineName.Vicissitude },
        Clan.Ventrue => new[] { DisciplineName.Dominate, DisciplineName.Fortitude, DisciplineName.Presence },
        _ => Array.Empty<DisciplineName>()
    };

    public static string DisplayName(DisciplineName d) => d switch
    {
        DisciplineName.BloodSorcery => "Blood Sorcery",
        DisciplineName.ThinBloodAlchemy => "Thin-Blood Alchemy",
        _ => d.ToString()
    };
}