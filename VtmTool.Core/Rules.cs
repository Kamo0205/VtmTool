using System;
using VtmTool.Core.Models;

namespace VtmTool.Core;

public static partial class Rules
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
        // V5 p.127: penalty when last 1, 2, or 3 boxes are filled
        int empty = max - total;
        return empty switch { 0 => -3, 1 => -2, 2 => -1, _ => 0 };
    }

    // V5 p.212 — Rouse check: roll 1d10, fail on 1–5.
    // Failure raises Hunger by 1. Hunger cannot exceed 5.
    // Returns updated Hunger value; caller writes it back.
    public static byte RouseCheck(in Character c, int roll) =>
        roll >= 6 ? c.Hunger : (byte)Math.Min(c.Hunger + 1, 5);

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

    // Recover aggravated health damage.
    public static Character HealAggravatedHealth(Character c, int amount)
    {
        c.AggravatedHealth = (byte)Math.Max(c.AggravatedHealth - amount, 0);
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

    public static Character ApplyAggravatedWillpower(Character c, int amount)
    {
        c.AggravatedWillpower = (byte)Math.Min(c.AggravatedWillpower + amount, c.WillpowerMax);
        int total = c.AggravatedWillpower + c.SuperficialWillpower;
        if (total > c.WillpowerMax)
            c.SuperficialWillpower = (byte)(c.WillpowerMax - c.AggravatedWillpower);
        return c;
    }

    // Recover superficial willpower damage.
    public static Character HealSuperficialWillpower(Character c, int amount)
    {
        c.SuperficialWillpower = (byte)Math.Max(c.SuperficialWillpower - amount, 0);
        return c;
    }

    // Recover aggravated willpower damage.
    public static Character HealAggravatedWillpower(Character c, int amount)
    {
        c.AggravatedWillpower = (byte)Math.Max(c.AggravatedWillpower - amount, 0);
        return c;
    }

    public static int XpCost(XpTraitType type, int toRating) => type switch
    {
        XpTraitType.Attribute => toRating * 5,
        XpTraitType.Skill => toRating * 3,
        XpTraitType.Specialty => 1,
        XpTraitType.InClanDiscipline => toRating * 5,
        XpTraitType.OutOfClanDiscipline => toRating * 7,
        XpTraitType.BloodPotency => toRating * 10,
        XpTraitType.Humanity => toRating * 3,
        XpTraitType.Background => toRating * 3,
        XpTraitType.Merit => toRating * 3,
        _ => 0,
    };
}