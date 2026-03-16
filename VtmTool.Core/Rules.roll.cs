// Add this method to the existing Rules static class in Rules.cs.
// No other changes to Rules.cs are needed.

using System;
using VtmTool.Core.Models;

namespace VtmTool.Core;

public static partial class Rules
{
    // V5 p.120–126 — Core dice resolution.
    //
    // poolSize    : total dice in the pool (after wound penalty and modifiers)
    // hungerCount : how many of those are hunger dice (= character's Hunger)
    // difficulty  : successes needed to succeed (default 1 for most checks)
    // poolLabel   : human-readable description for the result header
    // rng         : caller supplies so the same Random instance is reused
    public static RollResult Roll(int poolSize, int hungerCount, int difficulty,
                                  string poolLabel, Random rng)
    {
        poolSize = Math.Max(0, poolSize);
        hungerCount = Math.Clamp(hungerCount, 0, poolSize);
        int normalCount = poolSize - hungerCount;

        var normalDice = new int[normalCount];
        var hungerDice = new int[hungerCount];

        for (int i = 0; i < normalCount; i++) normalDice[i] = rng.Next(1, 11);
        for (int i = 0; i < hungerCount; i++) hungerDice[i] = rng.Next(1, 11);

        // Tally successes and special faces
        int successes = 0;
        int normalTens = 0;
        int hungerTens = 0;
        bool hasHungerOne = false;

        foreach (int d in normalDice)
        {
            if (d == 10) { normalTens++; successes += 2; }
            else if (d >= 6) successes++;
        }
        foreach (int d in hungerDice)
        {
            if (d == 10) { hungerTens++; successes += 2; }
            else if (d >= 6) successes++;
            else if (d == 1) hasHungerOne = true;
        }

        // V5 p.123 — Paired 10s (across normal and hunger combined) each add
        // 2 extra successes. We already counted 2 per 10 above, so a pair of
        // 10s = 4 total successes. Add the extra 2 per pair here.
        int pairs = (normalTens + hungerTens) / 2;
        successes += pairs * 2;

        bool isSuccess = successes >= difficulty;
        bool isBestial = !isSuccess && hasHungerOne;
        bool isMessy = isSuccess && hungerTens > 0;
        // Critical win: at least one pair of non-hunger 10s, no messy (hunger 10 takes priority)
        bool isCritical = isSuccess && normalTens >= 2 && !isMessy;

        string verdict = (isBestial, isMessy, isCritical, isSuccess) switch
        {
            (true, _, _, _) => "BESTIAL FAILURE — The Beast asserts itself",
            (_, true, _, _) => "MESSY CRITICAL — Success with a bloody edge",
            (_, _, true, _) => "CRITICAL WIN — Exceptional success",
            (_, _, _, true) => $"SUCCESS — {successes} success{(successes == 1 ? "" : "es")}",
            _ => "FAILURE",
        };

        return new RollResult
        {
            PoolSize = poolSize,
            HungerCount = hungerCount,
            Difficulty = difficulty,
            Successes = successes,
            NormalDice = normalDice,
            HungerDice = hungerDice,
            IsBestialFailure = isBestial,
            IsMessyCritical = isMessy,
            IsCriticalWin = isCritical,
            IsSuccess = isSuccess,
            PoolLabel = poolLabel,
            Verdict = verdict,
        };
    }
}