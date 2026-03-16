// Db.Xp.cs — merge into Db (must be declared partial).
// Add the two CREATE TABLE calls to Db.Init():
//   cmd.CommandText = CreateXpAwardTableSql;   cmd.ExecuteNonQuery();
//   cmd.CommandText = CreateXpPurchaseTableSql; cmd.ExecuteNonQuery();

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using VtmTool.Core.Models;

namespace VtmTool.Core;

public static partial class Db
{
    public const string CreateXpAwardTableSql = @"
        CREATE TABLE IF NOT EXISTS xp_award (
            id           INTEGER PRIMARY KEY,
            character_id INTEGER NOT NULL REFERENCES character(id) ON DELETE CASCADE,
            amount       INTEGER NOT NULL,
            note         TEXT    NOT NULL DEFAULT '',
            awarded_at   TEXT    NOT NULL
        );";

    public const string CreateXpPurchaseTableSql = @"
        CREATE TABLE IF NOT EXISTS xp_purchase (
            id           INTEGER PRIMARY KEY,
            character_id INTEGER NOT NULL REFERENCES character(id) ON DELETE CASCADE,
            trait_type   INTEGER NOT NULL,
            trait_name   TEXT    NOT NULL,
            from_rating  INTEGER NOT NULL,
            to_rating    INTEGER NOT NULL,
            cost         INTEGER NOT NULL,
            purchased_at TEXT    NOT NULL
        );";

    // ── Awards ────────────────────────────────────────────────────────────────

    public static XpAward AddAward(int characterId, int amount, string note)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        string now = DateTime.UtcNow.ToString("o");
        cmd.CommandText = @"
            INSERT INTO xp_award (character_id, amount, note, awarded_at)
            VALUES ($cid, $amt, $note, $at);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$cid", characterId);
        cmd.Parameters.AddWithValue("$amt", amount);
        cmd.Parameters.AddWithValue("$note", note ?? "");
        cmd.Parameters.AddWithValue("$at", now);
        int id = (int)(long)cmd.ExecuteScalar()!;
        return new XpAward
        {
            Id = id,
            CharacterId = characterId,
            Amount = amount,
            Note = note ?? "",
            AwardedAt = now,
        };
    }

    public static List<XpAward> LoadAwards(int characterId)
    {
        var list = new List<XpAward>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, character_id, amount, note, awarded_at " +
            "FROM xp_award WHERE character_id = $cid ORDER BY id;";
        cmd.Parameters.AddWithValue("$cid", characterId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new XpAward
            {
                Id = r.GetInt32(0),
                CharacterId = r.GetInt32(1),
                Amount = r.GetInt32(2),
                Note = r.GetString(3),
                AwardedAt = r.GetString(4),
            });
        return list;
    }

    // ── Purchases ─────────────────────────────────────────────────────────────

    public static XpPurchase AddPurchase(int characterId, XpTraitType traitType,
                                          string traitName, int from, int to, int cost)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        string now = DateTime.UtcNow.ToString("o");
        cmd.CommandText = @"
            INSERT INTO xp_purchase
                (character_id, trait_type, trait_name, from_rating, to_rating, cost, purchased_at)
            VALUES ($cid, $tt, $tn, $fr, $to, $cost, $at);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$cid", characterId);
        cmd.Parameters.AddWithValue("$tt", (byte)traitType);
        cmd.Parameters.AddWithValue("$tn", traitName);
        cmd.Parameters.AddWithValue("$fr", from);
        cmd.Parameters.AddWithValue("$to", to);
        cmd.Parameters.AddWithValue("$cost", cost);
        cmd.Parameters.AddWithValue("$at", now);
        int id = (int)(long)cmd.ExecuteScalar()!;
        return new XpPurchase
        {
            Id = id,
            CharacterId = characterId,
            TraitType = traitType,
            TraitName = traitName,
            FromRating = from,
            ToRating = to,
            Cost = cost,
            PurchasedAt = now,
        };
    }

    public static List<XpPurchase> LoadPurchases(int characterId)
    {
        var list = new List<XpPurchase>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, character_id, trait_type, trait_name, from_rating, to_rating, cost, purchased_at " +
            "FROM xp_purchase WHERE character_id = $cid ORDER BY id;";
        cmd.Parameters.AddWithValue("$cid", characterId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new XpPurchase
            {
                Id = r.GetInt32(0),
                CharacterId = r.GetInt32(1),
                TraitType = (XpTraitType)r.GetByte(2),
                TraitName = r.GetString(3),
                FromRating = r.GetInt32(4),
                ToRating = r.GetInt32(5),
                Cost = r.GetInt32(6),
                PurchasedAt = r.GetString(7),
            });
        return list;
    }

    // Load totals for all characters at startup — avoids N+1 queries.
    // Returns (totalEarned, totalSpent) keyed by character_id.
    public static Dictionary<int, (int Earned, int Spent)> LoadAllXpTotals()
    {
        var dict = new Dictionary<int, (int, int)>();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        // Awards
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT character_id, COALESCE(SUM(amount), 0) FROM xp_award GROUP BY character_id;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                dict[r.GetInt32(0)] = (r.GetInt32(1), 0);
        }

        // Purchases
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT character_id, COALESCE(SUM(cost), 0) FROM xp_purchase GROUP BY character_id;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int cid = r.GetInt32(0);
                int spent = r.GetInt32(1);
                dict[cid] = dict.TryGetValue(cid, out var existing)
                    ? (existing.Item1, spent)
                    : (0, spent);
            }
        }

        return dict;
    }
}