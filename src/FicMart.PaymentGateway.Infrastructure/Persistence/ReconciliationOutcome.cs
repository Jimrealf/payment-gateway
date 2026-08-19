namespace FicMart.PaymentGateway.Infrastructure.Persistence;

public enum ReconciliationOutcome
{
    Recovered = 1,
    NoActionRequired = 2,
    OperatorReviewRequired = 3,
    AlreadyProcessing = 4,
    StillPending = 5,
}
