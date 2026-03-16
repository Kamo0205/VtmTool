using VtmTool.Core.Models;

namespace VtmTool.Core;

public static partial class Rules
{
    // Convenience: compute available XP from award and purchase totals.
    public static int XpAvailable(int totalEarned, int totalSpent) =>
        totalEarned - totalSpent;
}