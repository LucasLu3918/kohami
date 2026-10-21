using Microsoft.Data.SqlClient;
using Kohami.Core;

static class Issue14Acceptance
{
    public static async Task PrintPaymentAttemptVersionAsync(string configPath, string paymentId)
    {
        var settings = RuntimeSettings.Load(configPath);
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
SELECT JSON_VALUE(Body,'$.version')
FROM commerce.Documents
WHERE Dataset='kohami_local' AND Kind='PaymentAttempt' AND Id=@payment;", connection);
        command.Parameters.AddWithValue("@payment", paymentId);
        var version = Convert.ToString(await command.ExecuteScalarAsync());
        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException("ISSUE14_PAYMENT_ATTEMPT_VERSION_MISSING");
        Console.WriteLine(version);
    }

    public static async Task ExpireCheckoutAsync(string configPath, string checkoutId)
    {
        var settings = RuntimeSettings.Load(configPath);
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
UPDATE commerce.Documents
SET Body=JSON_MODIFY(Body,'$.expiresAt',CONVERT(varchar(33),DATEADD(second,-1,SYSUTCDATETIME()),127)+'Z'),
    Version=Version+1
WHERE Dataset='kohami_local' AND Kind='Checkout' AND Id=@checkout
  AND JSON_VALUE(Body,'$.orderState')='AwaitingPayment';", connection);
        command.Parameters.AddWithValue("@checkout", checkoutId);
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("ISSUE14_CHECKOUT_NOT_EXPIRABLE");
        Console.WriteLine("PASS Issue14 fixture checkout deadline moved to the past in the dedicated SQL fixture");
    }

    public static async Task VerifyLateCompensationAsync(
        string configPath, string checkoutId, string orderId, string paymentId, string caseId, string refundOperationId)
    {
        var settings = RuntimeSettings.Load(configPath);
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
SELECT
  JSON_VALUE(c.Body,'$.orderState'), JSON_VALUE(c.Body,'$.paymentState'), JSON_VALUE(c.Body,'$.compensationRequired'),
  JSON_VALUE(o.Body,'$.orderState'), JSON_VALUE(p.Body,'$.state'), JSON_VALUE(rc.Body,'$.state'), JSON_VALUE(ro.Body,'$.state'),
  (SELECT COUNT(*) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order AND Kind='Late'),
  (SELECT COALESCE(SUM(Pending),0) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order),
  (SELECT COALESCE(SUM(Refunded),0) FROM commerce.Collections WHERE Dataset='kohami_local' AND OrderId=@order),
  (SELECT COUNT(*) FROM commerce.Reservations WHERE Dataset='kohami_local' AND OrderId=@order AND State='Released'),
  (SELECT COUNT(*) FROM commerce.Jobs WHERE Dataset='kohami_local' AND TargetId=@refund AND Kind='Refund' AND State='Completed')
FROM commerce.Documents c
JOIN commerce.Documents o ON o.Dataset=c.Dataset AND o.Kind='Order' AND o.Id=@order
JOIN commerce.Documents p ON p.Dataset=c.Dataset AND p.Kind='PaymentAttempt' AND p.Id=@payment
JOIN commerce.Documents rc ON rc.Dataset=c.Dataset AND rc.Kind='RefundCase' AND rc.Id=@case
JOIN commerce.Documents ro ON ro.Dataset=c.Dataset AND ro.Kind='RefundOperation' AND ro.Id=@refund
WHERE c.Dataset='kohami_local' AND c.Kind='Checkout' AND c.Id=@checkout;", connection);
        command.Parameters.AddWithValue("@checkout", checkoutId);
        command.Parameters.AddWithValue("@order", orderId);
        command.Parameters.AddWithValue("@payment", paymentId);
        command.Parameters.AddWithValue("@case", caseId);
        command.Parameters.AddWithValue("@refund", refundOperationId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("ISSUE14_PERSISTED_ROWS_MISSING");
        var valid = reader.GetString(0) == "Expired" && reader.GetString(1) == "Succeeded" &&
            reader.GetString(2) == "true" && reader.GetString(3) == "Expired" && reader.GetString(4) == "Succeeded" &&
            reader.GetString(5) == "Requested" && reader.GetString(6) == "Failed" &&
            reader.GetInt32(7) == 1 && Convert.ToInt64(reader.GetValue(8)) == 0 &&
            Convert.ToInt64(reader.GetValue(9)) == 0 && reader.GetInt32(10) == 1 && reader.GetInt32(11) == 1;
        if (!valid)
            throw new InvalidOperationException("ISSUE14_LATE_COMPENSATION_STATE_INVALID");
        Console.WriteLine("PASS Issue14 SQL state keeps the order expired, records one late collection, and releases failed compensation pending balance");
    }
}
