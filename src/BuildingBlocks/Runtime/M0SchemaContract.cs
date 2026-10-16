using Microsoft.Data.SqlClient;

namespace Kohami.M0.Core;

public static class M0SchemaContract
{
    public static IReadOnlyList<(string MigrationId, string SourceHash)> Supported { get; } =
    [
        ("0001_identity_commerce_baseline", "944b10b41d2722d15dd98a63b5e9d6b5f9fe1cd4bdaa513b214ad7cbee8ad141"),
        ("0002_native_operations", "c08ca2e224b334f183996bfdcc0830e2dd17c43da939a847c98554ec01dc966f"),
        ("0003_database_grants", "81adb1ff177d1714d07d07d69dca31ae9b8ec55f4ba5d9c28dfe3317a851eeb0"),
        ("0004_schema_readiness_grant", "7ae6647d7bf06ab1ff8e323f7129d02643d568619ae1cf6b3532ba18280c7da8"),
        ("0005_catalog", "7b78d62a9d4c8e259628c1d78e128ea638790ff355552ede8ce44e92688d9317"),
    ];

    public static bool IsCurrent(IEnumerable<(string MigrationId, string SourceHash)> applied) =>
        Supported.SequenceEqual(applied, MigrationComparer.Instance);

    public static async Task<bool> IsReadyAsync(M0Sql database, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(
                "SELECT MigrationId,SourceHash FROM ops.SchemaMigrations ORDER BY MigrationId", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var applied = new List<(string MigrationId, string SourceHash)>();
            while (await reader.ReadAsync(cancellationToken))
                applied.Add((reader.GetString(0), reader.GetString(1).Trim()));
            return IsCurrent(applied);
        }
        catch (SqlException)
        {
            return false;
        }
    }

    private sealed class MigrationComparer : IEqualityComparer<(string MigrationId, string SourceHash)>
    {
        public static readonly MigrationComparer Instance = new();

        public bool Equals((string MigrationId, string SourceHash) left,
            (string MigrationId, string SourceHash) right) =>
            StringComparer.Ordinal.Equals(left.MigrationId, right.MigrationId) &&
            StringComparer.Ordinal.Equals(left.SourceHash, right.SourceHash);

        public int GetHashCode((string MigrationId, string SourceHash) value) =>
            HashCode.Combine(StringComparer.Ordinal.GetHashCode(value.MigrationId),
                StringComparer.Ordinal.GetHashCode(value.SourceHash));
    }
}
