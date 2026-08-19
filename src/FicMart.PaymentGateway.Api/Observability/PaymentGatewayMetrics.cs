using System.Diagnostics.Metrics;

namespace FicMart.PaymentGateway.Api.Observability;

public sealed class PaymentGatewayMetrics : IDisposable
{
    public const string MeterName = "FicMart.PaymentGateway";
    private readonly Meter meter = new(MeterName);
    private readonly Counter<long> commands;
    private readonly Counter<long> reconciliations;
    private readonly Counter<long> bankAttempts;
    private readonly Counter<long> retries;
    private readonly Counter<long> replays;
    private readonly Histogram<double> bankLatency;

    public PaymentGatewayMetrics()
    {
        commands = meter.CreateCounter<long>("payment_gateway.commands");
        reconciliations = meter.CreateCounter<long>("payment_gateway.reconciliations");
        bankAttempts = meter.CreateCounter<long>("payment_gateway.bank_attempts");
        retries = meter.CreateCounter<long>("payment_gateway.retries");
        replays = meter.CreateCounter<long>("payment_gateway.idempotent_replays");
        bankLatency = meter.CreateHistogram<double>("payment_gateway.bank_latency", "ms");
    }

    public void RecordCommand(string operation) => commands.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void RecordReconciliation(string outcome) => reconciliations.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public void RecordBankAttempt(string operation, string outcome, double elapsedMilliseconds)
    {
        bankAttempts.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", outcome));
        bankLatency.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordRetry(string operation) => retries.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void RecordReplay(string operation) => replays.Add(1, new KeyValuePair<string, object?>("operation", operation));

    public void Dispose() => meter.Dispose();
}
