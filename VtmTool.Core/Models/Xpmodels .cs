namespace VtmTool.Core.Models;

/// <summary>One XP award from the ST.</summary>
public struct XpAward
{
    public int Id;
    public int CharacterId;
    public int Amount;
    public string Note;
    public string AwardedAt;   // ISO8601
}

/// <summary>One XP purchase — a single trait raise.</summary>
public struct XpPurchase
{
    public int Id;
    public int CharacterId;
    public XpTraitType TraitType;
    public string TraitName;   // "Strength", "Brawl", "Auspex", etc.
    public int FromRating;
    public int ToRating;
    public int Cost;
    public string PurchasedAt; // ISO8601
}