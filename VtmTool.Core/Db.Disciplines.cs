// Db.Disciplines.cs
// Add these members to the existing Db static class in Db.cs.
// Placed in a separate file for clarity — in C# a static class can be split
// across files using partial if you prefer, but since Db is not currently
// declared partial you can either:
//   (a) merge these methods directly into Db.cs, or
//   (b) declare Db as partial class and add this as Db.Disciplines.cs
//
// The schema addition and all four methods below are the complete discipline
// persistence layer. No other changes to Db.cs are needed.

using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using VtmTool.Core.Enums;
using VtmTool.Core.Models;

namespace VtmTool.Core;

// ── Schema addition ───────────────────────────────────────────────────────────
// Add this constant to Db and call it from Init() after CreateTableSql:
//
//   const string CreateDisciplineTableSql = Db.CreateDisciplineTableSql;
//   cmd.CommandText = CreateDisciplineTableSql;
//   cmd.ExecuteNonQuery();
//
// Or just append it to the Init() method body — one extra command execution.

public static partial class Db
{
    // Placed here so Init() can call it alongside CreateTableSql.
    public const string CreateDisciplineTableSql = @"
        CREATE TABLE IF NOT EXISTS discipline (
            id           INTEGER PRIMARY KEY,
            character_id INTEGER NOT NULL REFERENCES character(id) ON DELETE CASCADE,
            name         INTEGER NOT NULL,
            rating       INTEGER NOT NULL,
            UNIQUE(character_id, name)
        );";

    // Load all disciplines for one character.
    public static List<Discipline> LoadDisciplines(int characterId)
    {
        var list = new List<Discipline>(3);
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, character_id, name, rating FROM discipline " +
            "WHERE character_id = $cid ORDER BY name;";
        cmd.Parameters.AddWithValue("$cid", characterId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Discipline
            {
                Id = r.GetInt32(0),
                CharacterId = r.GetInt32(1),
                Name = (DisciplineName)r.GetByte(2),
                Rating = r.GetByte(3),
            });
        return list;
    }

    // Load disciplines for every character in one query.
    // Returns a dictionary keyed by character_id — used at startup to
    // populate _disciplines in parallel with _characters.
    public static Dictionary<int, List<Discipline>> LoadAllDisciplines()
    {
        var dict = new Dictionary<int, List<Discipline>>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, character_id, name, rating FROM discipline ORDER BY character_id, name;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int cid = r.GetInt32(1);
            if (!dict.TryGetValue(cid, out var list))
                dict[cid] = list = new List<Discipline>(3);
            list.Add(new Discipline
            {
                Id = r.GetInt32(0),
                CharacterId = cid,
                Name = (DisciplineName)r.GetByte(2),
                Rating = r.GetByte(3),
            });
        }
        return dict;
    }

    // Insert or update a single discipline (upsert).
    // Returns the discipline with Id filled in.
    public static Discipline SaveDiscipline(Discipline d)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        if (d.Id == 0)
        {
            // INSERT OR REPLACE handles the UNIQUE(character_id, name) constraint:
            // if a row with the same (character_id, name) already exists it is
            // replaced, avoiding a duplicate-key error.
            cmd.CommandText = @"
                INSERT OR REPLACE INTO discipline (character_id, name, rating)
                VALUES ($cid, $name, $rating);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$cid", d.CharacterId);
            cmd.Parameters.AddWithValue("$name", (byte)d.Name);
            cmd.Parameters.AddWithValue("$rating", d.Rating);
            d.Id = (int)(long)cmd.ExecuteScalar()!;
        }
        else
        {
            cmd.CommandText =
                "UPDATE discipline SET rating = $rating WHERE id = $id;";
            cmd.Parameters.AddWithValue("$rating", d.Rating);
            cmd.Parameters.AddWithValue("$id", d.Id);
            cmd.ExecuteNonQuery();
        }
        return d;
    }

    // Replace all disciplines for one character in a single transaction.
    // Called after the creation wizard or edit dialog commits a full set.
    public static List<Discipline> ReplaceAllDisciplines(int characterId, List<Discipline> disciplines)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Delete existing rows for this character
        using var del = conn.CreateCommand();
        del.Transaction = tx;
        del.CommandText = "DELETE FROM discipline WHERE character_id = $cid;";
        del.Parameters.AddWithValue("$cid", characterId);
        del.ExecuteNonQuery();

        // Insert the new set
        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = @"
            INSERT INTO discipline (character_id, name, rating)
            VALUES ($cid, $name, $rating);
            SELECT last_insert_rowid();";
        ins.Parameters.Add(new SqliteParameter("$cid", characterId));
        ins.Parameters.Add(new SqliteParameter("$name", (byte)0));
        ins.Parameters.Add(new SqliteParameter("$rating", (byte)0));

        var result = new List<Discipline>(disciplines.Count);
        foreach (var d in disciplines)
        {
            ins.Parameters["$name"].Value = (byte)d.Name;
            ins.Parameters["$rating"].Value = d.Rating;
            int newId = (int)(long)ins.ExecuteScalar()!;
            result.Add(d with { Id = newId, CharacterId = characterId });
        }

        tx.Commit();
        return result;
    }
}