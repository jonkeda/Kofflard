using Microsoft.Data.Sqlite;

namespace Kofflard.Infrastructure.Data;

/// <summary>
/// The app's SQLite file and the migrations that shape it (design §9).
/// </summary>
/// <remarks>
/// The schema version is SQLite's <c>user_version</c>; each migration runs in its own transaction and
/// moves it by one, as in Bruinet's Todo sample. Secrets are never stored here.
/// </remarks>
public sealed class KofflardDatabase(string path)
{
    /// <summary>The newest schema version.</summary>
    public const int LatestVersion = 1;

    private static readonly string[] Migrations =
    [
        // v1: settings, history, the result and verification caches, saved musicians.
        """
        CREATE TABLE settings (
            key    TEXT PRIMARY KEY,
            value  TEXT NOT NULL
        );
        CREATE TABLE searches (
            normalised  TEXT PRIMARY KEY,
            artist      TEXT NOT NULL,
            adventure   INTEGER NOT NULL,
            at          TEXT NOT NULL
        );
        CREATE TABLE shown (
            normalised      TEXT PRIMARY KEY,
            name            TEXT NOT NULL,
            first_shown_at  TEXT NOT NULL,
            last_shown_at   TEXT NOT NULL,
            opened          INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE result_cache (
            key   TEXT PRIMARY KEY,
            json  TEXT NOT NULL,
            at    TEXT NOT NULL
        );
        CREATE TABLE verified (
            normalised  TEXT PRIMARY KEY,
            state       TEXT NOT NULL,
            mbid        TEXT NULL,
            spotify_id  TEXT NULL,
            at          TEXT NOT NULL
        );
        CREATE TABLE saved (
            normalised  TEXT PRIMARY KEY,
            name        TEXT NOT NULL,
            why         TEXT NOT NULL,
            tags        TEXT NOT NULL,
            known_for   TEXT NULL,
            spotify_id  TEXT NULL,
            saved_at    TEXT NOT NULL
        );
        """,
    ];

    private readonly SemaphoreSlim _migrationGate = new(1, 1);
    private bool _migrated;

    /// <summary>The file's path.</summary>
    public string Path { get; } = path;

    /// <summary>An open connection on a migrated file. The caller disposes it.</summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        await MigrateAsync(cancellationToken);
        var connection = Connect();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Brings the file to <see cref="LatestVersion"/>; creates it when missing.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (_migrated)
        {
            return;
        }

        await _migrationGate.WaitAsync(cancellationToken);
        try
        {
            if (_migrated)
            {
                return;
            }

            if (System.IO.Path.GetDirectoryName(Path) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            await using var connection = Connect();
            await connection.OpenAsync(cancellationToken);

            var version = Convert.ToInt32(await Scalar(connection, "PRAGMA user_version;", cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            for (var next = version; next < LatestVersion; next++)
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = (SqliteTransaction)transaction;
                    command.CommandText = Migrations[next] + $"\nPRAGMA user_version = {next + 1};";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }

            _migrated = true;
        }
        finally
        {
            _migrationGate.Release();
        }
    }

    // No pooling: tests delete the file when they are done, which a pooled connection would keep open.
    private SqliteConnection Connect() => new(new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false }.ToString());

    private static async Task<object?> Scalar(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}

/// <summary>Short helpers for parameterised commands.</summary>
internal static class SqliteCommands
{
    /// <summary>A command with <paramref name="sql"/> and its parameters, named <c>$name</c>.</summary>
    public static SqliteCommand Command(this SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    /// <summary>A timestamp as stored: round-trip, UTC.</summary>
    public static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A stored timestamp read back.</summary>
    public static DateTimeOffset ReadStamp(string stamp)
        => DateTimeOffset.Parse(stamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
}
