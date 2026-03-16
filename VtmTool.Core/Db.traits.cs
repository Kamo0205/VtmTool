// Db.Traits.cs — merge into Db (must be declared partial).
// Add to Db.Init():
//   cmd.CommandText = CreateTraitTableSql; cmd.ExecuteNonQuery();

using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using VtmTool.Core.Models;

namespace VtmTool.Core;

public static partial class Db
{
    public const string CreateTraitTableSql = @"
        CREATE TABLE IF NOT EXISTS trait (
            id           INTEGER PRIMARY KEY,
            character_id INTEGER NOT NULL REFERENCES character(id) ON DELETE CASCADE,
            category     INTEGER NOT NULL,
            name         TEXT    NOT NULL,
            rating       INTEGER NOT NULL DEFAULT 0,
            detail       TEXT    NOT NULL DEFAULT '',
            is_custom    INTEGER NOT NULL DEFAULT 0
        );";

    // ── Load ──────────────────────────────────────────────────────────────────

    public static List<CharacterTrait> LoadTraits(int characterId)
    {
        var list = new List<CharacterTrait>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, character_id, category, name, rating, detail, is_custom " +
            "FROM trait WHERE character_id = $cid ORDER BY category, name;";
        cmd.Parameters.AddWithValue("$cid", characterId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(ReadTrait(r));
        return list;
    }

    public static Dictionary<int, List<CharacterTrait>> LoadAllTraits()
    {
        var dict = new Dictionary<int, List<CharacterTrait>>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, character_id, category, name, rating, detail, is_custom " +
            "FROM trait ORDER BY character_id, category, name;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var t = ReadTrait(r);
            int cid = t.CharacterId;
            if (!dict.TryGetValue(cid, out var list))
                dict[cid] = list = new List<CharacterTrait>();
            list.Add(t);
        }
        return dict;
    }

    // ── Save (insert or update) ───────────────────────────────────────────────

    public static CharacterTrait SaveTrait(CharacterTrait t)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        if (t.Id == 0)
        {
            cmd.CommandText = @"
                INSERT INTO trait (character_id, category, name, rating, detail, is_custom)
                VALUES ($cid, $cat, $name, $rating, $detail, $custom);
                SELECT last_insert_rowid();";
            BindTrait(cmd, t);
            t.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText = @"
                UPDATE trait SET
                    category  = $cat,
                    name      = $name,
                    rating    = $rating,
                    detail    = $detail,
                    is_custom = $custom
                WHERE id = $id;";
            BindTrait(cmd, t);
            cmd.Parameters.AddWithValue("$id", t.Id);
            cmd.ExecuteNonQuery();
        }
        return t;
    }

    public static void DeleteTrait(int id)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM trait WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void BindTrait(SqliteCommand cmd, CharacterTrait t)
    {
        cmd.Parameters.AddWithValue("$cid", t.CharacterId);
        cmd.Parameters.AddWithValue("$cat", (byte)t.Category);
        cmd.Parameters.AddWithValue("$name", t.Name ?? "");
        cmd.Parameters.AddWithValue("$rating", t.Rating);
        cmd.Parameters.AddWithValue("$detail", t.Detail ?? "");
        cmd.Parameters.AddWithValue("$custom", t.IsCustom ? 1 : 0);
    }

    static CharacterTrait ReadTrait(SqliteDataReader r) => new CharacterTrait
    {
        Id = r.GetInt32(0),
        CharacterId = r.GetInt32(1),
        Category = (TraitCategory)r.GetByte(2),
        Name = r.GetString(3),
        Rating = r.GetByte(4),
        Detail = r.GetString(5),
        IsCustom = r.GetInt32(6) == 1,
    };
}