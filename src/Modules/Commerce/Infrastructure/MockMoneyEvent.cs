namespace Kohami.Commerce;

/// <summary>The versioned, synthetic provider envelope consumed by the Commerce worker.</summary>
public sealed record MockMoneyEvent(
    string EventId,
    string Mode,
    string Dataset,
    string OrderId,
    string OperationId,
    long AmountTwd,
    string Currency,
    int ContractVersion,
    string Outcome);
