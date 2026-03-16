using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using VtmTool.Core.Models;

namespace VtmTool.Core;

public static partial class Db
{
    // ── Schema ────────────────────────────────────────────────────────────────
    // Add these three columns to the character table.
    // ALTER TABLE ADD COLUMN is safe on existing SQLite databases — it adds
    // the column with the default value for all existing rows.
    // Call all three from Db.Init() after the CREATE TABLE statement.
    //
    //   RunIfColumnMissing("notes_background", "ALTER TABLE character ADD COLUMN notes_background TEXT NOT NULL DEFAULT ''");
    //   RunIfColumnMissing("notes_goals",      "ALTER TABLE character ADD COLUMN notes_goals      TEXT NOT NULL DEFAULT ''");
    //   RunIfColumnMissing("notes_coterie",    "ALTER TABLE character ADD COLUMN notes_coterie    TEXT NOT NULL DEFAULT ''");
    //
    // Use the helper below so Init() is idempotent — safe to call on both
    // new and existing databases.

    // Add this helper to Db (private, used only by Init):
    // static void RunIfColumnMissing(string column, string alterSql)
    // {
    //     using var conn = new SqliteConnection(ConnStr);
    //     conn.Open();
    //     using var check = conn.CreateCommand();
    //     check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('character') WHERE name = '{column}';";
    //     long count = (long)check.ExecuteScalar()!;
    //     if (count == 0)
    //     {
    //         using var alter = conn.CreateCommand();
    //         alter.CommandText = alterSql;
    //         alter.ExecuteNonQuery();
    //     }
    // }

    // Also add these three columns to CreateTableSql for fresh databases:
    //   notes_background  TEXT NOT NULL DEFAULT '',
    //   notes_goals       TEXT NOT NULL DEFAULT '',
    //   notes_coterie     TEXT NOT NULL DEFAULT '',

    // ── Load ──────────────────────────────────────────────────────────────────
    public static CharacterNotes LoadNotes(int characterId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT notes_background, notes_goals, notes_coterie " +
            "FROM character WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", characterId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return default;
        return new CharacterNotes
        {
            Background = r.GetString(0),
            Goals = r.GetString(1),
            Coterie = r.GetString(2),
        };
    }

    // Load notes for every character in one query.
    // Returns a dictionary keyed by character id.
    public static Dictionary<int, CharacterNotes> LoadAllNotes()
    {
        var dict = new Dictionary<int, CharacterNotes>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, notes_background, notes_goals, notes_coterie FROM character;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            dict[r.GetInt32(0)] = new CharacterNotes
            {
                Background = r.GetString(1),
                Goals = r.GetString(2),
                Coterie = r.GetString(3),
            };
        }
        return dict;
    }

    // ── Save ──────────────────────────────────────────────────────────────────
    public static void SaveNotes(int characterId, CharacterNotes notes)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE character SET
                notes_background = $background,
                notes_goals      = $goals,
                notes_coterie    = $coterie
            WHERE id = $id;";
        cmd.Parameters.AddWithValue("$background", notes.Background ?? "");
        cmd.Parameters.AddWithValue("$goals", notes.Goals ?? "");
        cmd.Parameters.AddWithValue("$coterie", notes.Coterie ?? "");
        cmd.Parameters.AddWithValue("$id", characterId);
        cmd.ExecuteNonQuery();
    }
}