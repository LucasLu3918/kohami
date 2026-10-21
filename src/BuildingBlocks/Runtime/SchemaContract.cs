using Microsoft.Data.SqlClient;

namespace Kohami.Core;

public static class SchemaContract
{
    public static IReadOnlyList<(string MigrationId, string SourceHash)> Supported { get; } =
    [
        ("0001_identity_commerce_baseline", "944b10b41d2722d15dd98a63b5e9d6b5f9fe1cd4bdaa513b214ad7cbee8ad141"),
        ("0002_native_operations", "c08ca2e224b334f183996bfdcc0830e2dd17c43da939a847c98554ec01dc966f"),
        ("0003_database_grants", "81adb1ff177d1714d07d07d69dca31ae9b8ec55f4ba5d9c28dfe3317a851eeb0"),
        ("0004_schema_readiness_grant", "7ae6647d7bf06ab1ff8e323f7129d02643d568619ae1cf6b3532ba18280c7da8"),
        ("0005_catalog", "7b78d62a9d4c8e259628c1d78e128ea638790ff355552ede8ce44e92688d9317"),
        ("0006_catalog_authorization_recheck", "3bd234ad424f48e8f0d7d82553556ea4e49ca363f17eb8a31574e892b45cc525"),
        ("0007_catalog_demo_showcase", "ca77cd71f78cc02abd870d8aca602d290229442d610ab78718a9a4b0d8f36618"),
        ("0008_password_only_admin_auth", "7d0c09c5cc341330b0db49a683dc108ac8492d3a72961147fabd7f4ab716d695"),
        ("0009_issue23_retention_audit", "566d2fa09326ed4eb56acea8f30ee35f37fd41ca0702af3866ff1d88d0a65bae"),
        ("0010_issue23_account_activity", "ac06d6041229ac503ab1dd9f2cf1b09f2d9152ee4d2a03328a84849d1710beac"),
        ("0011_issue23_account_inactivity", "61b2936b9de59f83db10686b509f4abe5c1939b587d340211dcf948c1561b527"),
        ("0012_issue23_guest_proof_retention", "5f3ec3dfe337d8ae1cfe029145182ec8c0bf2964422b4a0a56fa70df1f88a921"),
        ("0013_member_wallet_ledger", "48d6dd679bb1f0a0c59750d68fa19d70e2bc6db1bbfaea9af35be6a1af47f935"),
        ("0014_wallet_payment_api_writes", "d8d21fa137ecda4442865dcf53c655d39ddc1e89e9c52987ccf8057ba3d9ff12"),
        ("0015_catalog_admin_read", "b258012939bd42fb10c21ac70469e3f04ead867c067fc7584e5f44c7874d8ac0"),
        ("0016_kohami_identity_and_dataset_rename", "5ac322be926cb0e39834da142fb7257825498cb1515cdef7e2351b9f36190f9d"),
        ("0017_catalog_public_read_permissions", "cf383b0a3737fe9e3cee7328a2ded1c9cae77e5c6654f54f1951f11c43eb0613"),
        ("0018_api_schema_readiness", "dda6e7fb817e4caec821b31b69f66f2889d65dd146650812995621574c5b72dd"),
        ("0019_runtime_service_permissions", "8894bf253e2c29268794e3cda364647da6c8ea93fc9526e0f6410c12c34fabd8"),
        ("0020_member_registration", "a965e8b18bb0be154d4763eddedb35e1b0aedc4dc580c544f3d3df79d690a56c"),
        ("0021_identity_security_lock", "c97ea100f4e5039430a51ce999ec72ecf288ed9eccaa3d07c68fd21b9a352db8"),
    ];

    public static bool IsCurrent(IEnumerable<(string MigrationId, string SourceHash)> applied) =>
        Supported.SequenceEqual(applied, MigrationComparer.Instance);

    public static async Task<bool> IsReadyAsync(SqlRuntime database, CancellationToken cancellationToken = default)
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
