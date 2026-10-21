using Microsoft.Data.SqlClient;

namespace Kohami.Native;

public static class NativeSqlTarget
{
    public static string BuildConnectionString(
        string runtimeConnectionString,
        string nativePassword,
        string serverCertificatePath,
        string hostNameInCertificate)
    {
        var builder = new SqlConnectionStringBuilder(runtimeConnectionString);
        if (!IsDedicatedLocalEndpoint(builder.DataSource)
            || !string.Equals(builder.InitialCatalog, "Kohami", StringComparison.Ordinal)
            || builder.TrustServerCertificate
            || builder.Encrypt != SqlConnectionEncryptOption.Strict
            || builder.IntegratedSecurity
            || string.IsNullOrWhiteSpace(nativePassword)
            || string.IsNullOrWhiteSpace(serverCertificatePath)
            || !string.Equals(hostNameInCertificate, "localhost", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("NATIVE_SQL_TARGET_INVALID");
        }

        builder.UserID = "kohami_native";
        builder.Password = nativePassword;
        builder.Encrypt = SqlConnectionEncryptOption.Strict;
        builder.TrustServerCertificate = false;
        builder.HostNameInCertificate = hostNameInCertificate;
        builder["Server Certificate"] = serverCertificatePath;
        builder.ApplicationName = "KOHAMI-NativeAuthority";
        return builder.ConnectionString;
    }

    public static bool IsDedicatedLocalEndpoint(string dataSource)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            dataSource,
            @"\Alocalhost,(?<port>\d{1,5})\z",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success
            && int.TryParse(match.Groups["port"].Value, out var port)
            && port is >= 14336 and <= 14399;
    }
}
