using VtmTool.Core.Enums;

namespace VtmTool.Core.Models;

public struct Discipline
{
    public int Id;
    public int CharacterId;
    public DisciplineName Name;
    public byte Rating;      // 1–5
}