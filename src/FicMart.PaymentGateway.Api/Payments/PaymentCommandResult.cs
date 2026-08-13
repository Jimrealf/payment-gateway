namespace FicMart.PaymentGateway.Api.Payments;

public sealed record PaymentCommandResult(
    int StatusCode,
    PaymentResponse? Payment,
    PaymentErrorResponse? Error)
{
    public static PaymentCommandResult Success(int statusCode, PaymentResponse payment) =>
        new(statusCode, payment, null);

    public static PaymentCommandResult Failure(int statusCode, PaymentErrorResponse error) =>
        new(statusCode, null, error);
}
