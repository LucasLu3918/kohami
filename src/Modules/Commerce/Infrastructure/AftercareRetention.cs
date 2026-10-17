using System.Globalization;
using System.Text.Json.Nodes;

namespace Kohami.M0.Commerce;

internal static class AftercareRetention
{
    static readonly string[] ShipmentTerminalStates = ["Delivered", "Returned"];

    public static bool TryRedactShipment(CommerceDocument document, DateTimeOffset now, out CommerceDocument redacted)
    {
        redacted = document;
        if (document.Kind != "Shipment" || document.Body["retention"] is not null)
            return false;

        var state = Text(document.Body, "state");
        if (state is null || !ShipmentTerminalStates.Contains(state, StringComparer.Ordinal) ||
            !TryLastEventAt(document.Body, state, out var closedAt) ||
            closedAt >= now.AddDays(-180))
            return false;

        var history = SanitizedHistory(document.Body);
        var body = new JsonObject
        {
            ["id"] = Text(document.Body, "id"),
            ["orderId"] = Text(document.Body, "orderId"),
            ["mode"] = Text(document.Body, "mode"),
            ["state"] = state,
            ["version"] = CommerceWire.Version(document.Kind, document.Id, document.Version + 1),
            ["createdAt"] = UtcString(document.Body["createdAt"]),
            ["history"] = history,
            ["retention"] = RetentionStamp("delivery_180_days", closedAt, now)
        };

        redacted = document with { Owner = "retention_redacted", Version = document.Version + 1, Body = body };
        return true;
    }

    public static bool TryRedactRejectedReturn(CommerceDocument document, DateTimeOffset now, bool hasLinkedAftercareEvidence, out CommerceDocument redacted)
    {
        redacted = document;
        if (document.Kind != "Return" || document.Body["retention"] is not null || hasLinkedAftercareEvidence ||
            Text(document.Body, "state") != "Rejected" ||
            !TryLastEventAt(document.Body, "Rejected", out var closedAt) ||
            closedAt >= now.AddYears(-1))
            return false;

        var body = new JsonObject
        {
            ["id"] = Text(document.Body, "id"),
            ["orderId"] = Text(document.Body, "orderId"),
            ["orderLineId"] = Text(document.Body, "orderLineId"),
            ["quantity"] = Number(document.Body, "quantity"),
            ["mode"] = Text(document.Body, "mode"),
            ["state"] = "Rejected",
            ["version"] = CommerceWire.Version(document.Kind, document.Id, document.Version + 1),
            ["createdAt"] = UtcString(document.Body["createdAt"]),
            ["history"] = SanitizedHistory(document.Body),
            ["retention"] = RetentionStamp("customer_care_1_year", closedAt, now)
        };

        redacted = document with { Owner = "retention_redacted", Version = document.Version + 1, Body = body };
        return true;
    }

    public static bool TryMarkInspectedReturnClosed(
        CommerceDocument document, string refundCaseId, string refundOperationId, DateTimeOffset closedAt,
        out CommerceDocument closed)
    {
        closed = document;
        if (document.Kind != "Return" || document.Body["retention"] is not null ||
            Text(document.Body, "state") != "Inspected" ||
            Text(document.Body["inspection"] as JsonObject, "refundCaseId") != refundCaseId ||
            document.Body["closure"] is not null || string.IsNullOrWhiteSpace(refundOperationId))
            return false;

        var body = (JsonObject)document.Body.DeepClone();
        body["closure"] = new JsonObject
        {
            ["status"] = "Closed",
            ["basis"] = "RefundSucceeded",
            ["closedAt"] = closedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["refundCaseId"] = refundCaseId,
            ["refundOperationId"] = refundOperationId
        };
        closed = document with { Version = document.Version + 1, Body = body };
        return true;
    }

    public static bool TryRedactInspectedReturn(CommerceDocument document, DateTimeOffset now, bool hasValidClosureEvidence, out CommerceDocument redacted)
    {
        redacted = document;
        var closure = document.Body["closure"] as JsonObject;
        if (document.Kind != "Return" || document.Body["retention"] is not null || !hasValidClosureEvidence ||
            Text(document.Body, "state") != "Inspected" ||
            Text(closure, "status") != "Closed" || Text(closure, "basis") != "RefundSucceeded" ||
            Text(document.Body["inspection"] as JsonObject, "refundCaseId") != Text(closure, "refundCaseId") ||
            !TryUtc(closure?["closedAt"], out var closedAt) ||
            closedAt >= now.AddYears(-1) ||
            Text(closure, "refundOperationId") is not { Length: > 0 } operationId ||
            Text(closure, "refundCaseId") is not { Length: > 0 } caseId)
            return false;

        var body = new JsonObject
        {
            ["id"] = Text(document.Body, "id"),
            ["orderId"] = Text(document.Body, "orderId"),
            ["orderLineId"] = Text(document.Body, "orderLineId"),
            ["quantity"] = Number(document.Body, "quantity"),
            ["mode"] = Text(document.Body, "mode"),
            ["state"] = "Inspected",
            ["version"] = CommerceWire.Version(document.Kind, document.Id, document.Version + 1),
            ["createdAt"] = UtcString(document.Body["createdAt"]),
            ["history"] = SanitizedHistory(document.Body),
            ["closure"] = new JsonObject
            {
                ["status"] = "Closed",
                ["basis"] = "RefundSucceeded",
                ["closedAt"] = closedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                ["refundCaseId"] = caseId,
                ["refundOperationId"] = operationId
            },
            ["retention"] = RetentionStamp("customer_care_1_year", closedAt, now)
        };

        redacted = document with { Owner = "retention_redacted", Version = document.Version + 1, Body = body };
        return true;
    }

    static JsonArray SanitizedHistory(JsonObject body)
    {
        var result = new JsonArray();
        if (body["history"] is not JsonArray history)
            return result;

        foreach (var item in history.OfType<JsonObject>())
        {
            if (Text(item, "state") is not { Length: > 0 } state || UtcString(item["at"]) is not { Length: > 0 } at)
                continue;
            result.Add(new JsonObject { ["state"] = state, ["at"] = at });
        }
        return result;
    }

    static bool TryLastEventAt(JsonObject body, string terminalState, out DateTimeOffset at)
    {
        at = default;
        if (body["history"] is not JsonArray { Count: > 0 } history || history[^1] is not JsonObject last ||
            Text(last, "state") != terminalState || !TryUtc(last["at"], out at))
            return false;
        return true;
    }

    static JsonObject RetentionStamp(string policy, DateTimeOffset closedAt, DateTimeOffset redactedAt) => new()
    {
        ["status"] = "Redacted",
        ["policy"] = policy,
        ["closedAt"] = closedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        ["redactedAt"] = redactedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
    };

    static string? UtcString(JsonNode? node) => TryUtc(node, out var value)
        ? value.ToString("O", CultureInfo.InvariantCulture)
        : null;

    static bool TryUtc(JsonNode? node, out DateTimeOffset value)
    {
        value = default;
        if (node is not JsonValue json)
            return false;
        if (json.TryGetValue<DateTimeOffset>(out var instant))
        {
            value = instant.ToUniversalTime();
            return true;
        }
        if (json.TryGetValue<string>(out var text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out instant))
        {
            value = instant.ToUniversalTime();
            return true;
        }
        return false;
    }

    static string? Text(JsonObject? body, string name) => body?[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    static long? Number(JsonObject body, string name) => body[name] is JsonValue value && value.TryGetValue<long>(out var number) ? number : null;
}
