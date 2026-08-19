namespace FicMart.PaymentGateway.Infrastructure.Bank;

public interface IBankClient
{
    Task<BankAuthorizationResult> AuthorizeAsync(
        BankAuthorizationRequest request,
        CancellationToken cancellationToken);

    Task<BankCaptureResult> CaptureAsync(
        BankCaptureRequest request,
        CancellationToken cancellationToken);

    Task<BankVoidResult> VoidAsync(BankVoidRequest request, CancellationToken cancellationToken);

    Task<BankRefundResult> RefundAsync(BankRefundRequest request, CancellationToken cancellationToken);
}
