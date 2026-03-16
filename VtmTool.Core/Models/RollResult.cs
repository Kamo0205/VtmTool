namespace VtmTool.Core.Models;

/// <summary>
/// Result of one V5 dice roll. Computed by Rules.Roll(), displayed in the
/// roller panel, stored briefly in history. Never persisted to DB.
/// </summary>
public struct RollResult
{
    public int PoolSize;         // total dice rolled
    public int HungerCount;      // how many were hunger dice
    public int Difficulty;       // successes needed
    public int Successes;        // total successes scored

    public int[] NormalDice;       // values of non-hunger dice
    public int[] HungerDice;       // values of hunger dice

    // Outcome flags — checked in priority order:
    //   BestialFailure > MessyCritical > CriticalWin > Success > Failure
    public bool IsBestialFailure;   // 0 successes AND ≥1 hunger die shows 1
    public bool IsMessyCritical;    // succeeded AND ≥1 hunger die shows 10
    public bool IsCriticalWin;      // succeeded AND paired non-hunger 10s (no messy)
    public bool IsSuccess;          // Successes >= Difficulty

    // Human-readable pool description e.g. "Strength 2 + Brawl 3 − 1 = 4"
    public string PoolLabel;

    // One-line verdict shown after the dice
    public string Verdict;
}