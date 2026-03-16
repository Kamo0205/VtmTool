// Db.PredatorType.cs
// Merge into Db (which must be declared partial).
// Add the two ALTER TABLE calls to Db.Init() and the two columns to CreateTableSql.

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using VtmTool.Core.Models;

namespace VtmTool.Core;

public static partial class Db
{
    // ── Schema additions ──────────────────────────────────────────────────────
    // Add to CreateTableSql (fresh databases):
    //   predator_type  INTEGER NOT NULL DEFAULT 0,
    //   specialties    TEXT    NOT NULL DEFAULT '',
    //
    // Add to Init() after existing RunIfColumnMissing calls:
    //   RunIfColumnMissing("predator_type",
    //       "ALTER TABLE character ADD COLUMN predator_type INTEGER NOT NULL DEFAULT 0");
    //   RunIfColumnMissing("specialties",
    //       "ALTER TABLE character ADD COLUMN specialties TEXT NOT NULL DEFAULT ''");

    // ── Save predator type and specialties ────────────────────────────────────
    // Specialties stored as newline-separated "Skill: Label" strings.
    // Called from CreationWizard after character is saved and from any future
    // edit flow that changes predator type.
    public static void SavePredatorType(int characterId,
                                         PredatorType predatorType,
                                         string[] specialties)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE character SET
                predator_type = $pt,
                specialties   = $sp
            WHERE id = $id;";
        cmd.Parameters.AddWithValue("$pt", (byte)predatorType);
        cmd.Parameters.AddWithValue("$sp", string.Join("\n", specialties));
        cmd.Parameters.AddWithValue("$id", characterId);
        cmd.ExecuteNonQuery();
    }

    // ── Load specialties for one character ────────────────────────────────────
    public static string[] LoadSpecialties(int characterId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT specialties FROM character WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", characterId);
        var raw = cmd.ExecuteScalar() as string ?? "";
        return string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    // ── Load all specialties at startup ───────────────────────────────────────
    public static Dictionary<int, string[]> LoadAllSpecialties()
    {
        var dict = new Dictionary<int, string[]>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, specialties FROM character;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string raw = r.IsDBNull(1) ? "" : r.GetString(1);
            dict[r.GetInt32(0)] = string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        }
        return dict;
    }
}