using Comven_VehicleBilling.Application.Abstractions.Payments;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Invoices;

namespace Comven_VehicleBilling.UnitTests.Invoices;

public sealed class InvoicePaymentServiceTests
{
    private readonly FakeInvoiceRepository _repository = new();
    private readonly FakePaymentProvider _provider = new();
    private InvoicePaymentService CreateService() =>
        new(_repository, _provider);

    [Theory]
    [InlineData(PaymentOutcome.Approved, "Paid")]
    [InlineData(PaymentOutcome.Rejected, "PaymentFailed")]
    [InlineData(PaymentOutcome.Unknown, "PaymentUnknown")]
    public async Task PayAsync_ShouldProcessAndPersistProviderOutcome(
        PaymentOutcome outcome,
        string expectedStatus)
    {
        _provider.Result = new PaymentResult(outcome, "external-001", null);

        var result = await CreateService().PayAsync(_repository.Invoice.Id);

        Assert.Equal(1, _provider.Calls);
        Assert.Equal(1, _repository.StartPaymentCalls);
        Assert.Equal(1, _repository.CompletePaymentCalls);
        Assert.Equal(outcome, _repository.LastOutcome);
        Assert.Equal(expectedStatus, result.Status);

        Assert.NotNull(_provider.LastRequest);
        Assert.Equal(_repository.Invoice.Id, _provider.LastRequest.InvoiceId);
        Assert.Equal(_repository.Invoice.CustomerId, _provider.LastRequest.CustomerId);
        Assert.Equal(_repository.Invoice.Amount, _provider.LastRequest.Amount);
        Assert.Equal(
            _repository.Invoice.Id.ToString("N"),
            _provider.LastRequest.IdempotencyKey);
    }

    [Fact]
    public async Task PayAsync_ShouldNotCallProviderWhenPaymentCannotStart()
    {
        _repository.StartPaymentResult = null;

        var result = await CreateService().PayAsync(_repository.Invoice.Id);

        var expectedInvoice = _repository.ExistingInvoice
            ?? throw new InvalidOperationException("Fatura esperada não configurada.");

        Assert.Equal(expectedInvoice.Id, result.Id);
        Assert.Equal(0, _provider.Calls);
        Assert.Equal(0, _repository.CompletePaymentCalls);
        Assert.Equal(1, _repository.GetByIdCalls);
    }

    [Fact]
    public async Task PayAsync_ShouldThrowWhenInvoiceDoesNotExist()
    {
        _repository.StartPaymentResult = null;
        _repository.ExistingInvoice = null;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().PayAsync(Guid.NewGuid()));

        Assert.Equal("Fatura não encontrada.", exception.Message);
        Assert.Equal(0, _provider.Calls);
        Assert.Equal(0, _repository.CompletePaymentCalls);
    }

    [Fact]
    public async Task PayAsync_ShouldMarkPaymentUnknownWhenProviderThrows()
    {
        var expectedException = new InvalidOperationException("Provider unavailable");
        _provider.ExceptionToThrow = expectedException;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().PayAsync(_repository.Invoice.Id));

        Assert.Same(expectedException, exception);
        Assert.Equal(1, _provider.Calls);
        Assert.Equal(1, _repository.CompletePaymentCalls);
        Assert.Equal(PaymentOutcome.Unknown, _repository.LastOutcome);
        Assert.Equal(CancellationToken.None, _repository.LastCompletionToken);
    }

    [Fact]
    public async Task PayAsync_ShouldNotCallProviderWhenInvoiceIsNotProcessing()
    {
        var invoice = FakeInvoiceRepository.CreateInvoice("PaymentPending");
        _repository.StartPaymentResult = invoice;

        var result = await CreateService().PayAsync(invoice.Id);

        Assert.Same(invoice, result);
        Assert.Equal(0, _provider.Calls);
        Assert.Equal(0, _repository.CompletePaymentCalls);
    }

    [Fact]
    public async Task PayAsync_ShouldRejectEmptyInvoiceId()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().PayAsync(Guid.Empty));

        Assert.Equal(0, _repository.StartPaymentCalls);
        Assert.Equal(0, _provider.Calls);
    }


    [Fact]
    public void Constructor_ShouldRejectNullRepository()
    {
        Assert.Throws<ArgumentNullException>(
            () => new InvoicePaymentService(null!, _provider));
    }

    [Fact]
    public void Constructor_ShouldRejectNullPaymentProvider()
    {
        Assert.Throws<ArgumentNullException>(
            () => new InvoicePaymentService(_repository, null!));
    }

    [Fact]
    public async Task PayAsync_ShouldForwardCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        await CreateService().PayAsync(
            _repository.Invoice.Id,
            token);

        Assert.Equal(token, _repository.LastStartPaymentToken);
        Assert.Equal(token, _provider.LastCancellationToken);
        Assert.Equal(token, _repository.LastCompletionToken);
    }

    [Fact]
    public async Task PayAsync_ShouldForwardCancellationTokenWhenPaymentCannotStart()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        _repository.StartPaymentResult = null;

        await CreateService().PayAsync(
            _repository.Invoice.Id,
            token);

        Assert.Equal(token, _repository.LastStartPaymentToken);
        Assert.Equal(token, _repository.LastGetByIdToken);
        Assert.Equal(0, _provider.Calls);
    }
    private sealed class FakePaymentProvider : IPaymentProvider
    {
        public int Calls { get; private set; }

        public PaymentRequest? LastRequest { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public PaymentResult Result { get; set; } =
            new(PaymentOutcome.Approved, "external-001", null);

        public Exception? ExceptionToThrow { get; set; }

        public Task<PaymentResult> ProcessPaymentAsync(
            PaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            LastCancellationToken = cancellationToken;

            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeInvoiceRepository : IInvoiceRepository
    {
        public InvoiceDto Invoice { get; } =
            CreateInvoice("PaymentProcessing");

        public InvoiceDto? ExistingInvoice { get; set; } =
            CreateInvoice("PaymentPending");

        public InvoiceDto? StartPaymentResult { get; set; }

        public int StartPaymentCalls { get; private set; }

        public int GetByIdCalls { get; private set; }

        public int CompletePaymentCalls { get; private set; }

        public PaymentOutcome? LastOutcome { get; private set; }

        public CancellationToken LastCompletionToken { get; private set; }

        public CancellationToken LastStartPaymentToken { get; private set; }

        public CancellationToken LastGetByIdToken { get; private set; }

        public FakeInvoiceRepository()
        {
            StartPaymentResult = Invoice;
        }

        public static InvoiceDto CreateInvoice(string status) =>
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 31),
                12.50m,
                status,
                DateTimeOffset.UtcNow,
                null,
                Array.Empty<InvoiceItemDto>());

        public Task<InvoiceDto> CreateAsync(
            CreateInvoiceCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<InvoiceDto> CreateSupplementaryAsync(
            CreateSupplementaryInvoiceCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<InvoiceDto>> GetAllAsync(
            Guid? customerId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<InvoiceDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            LastGetByIdToken = cancellationToken;
            return Task.FromResult(ExistingInvoice);
        }

        public Task<InvoiceDto?> StartPaymentAsync(
            Guid invoiceId,
            CancellationToken cancellationToken = default)
        {
            StartPaymentCalls++;
            LastStartPaymentToken = cancellationToken;
            return Task.FromResult(StartPaymentResult);
        }

        public Task<InvoiceDto> CompletePaymentAsync(
            Guid invoiceId,
            PaymentOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            CompletePaymentCalls++;
            LastOutcome = outcome;
            LastCompletionToken = cancellationToken;

            var status = outcome switch
            {
                PaymentOutcome.Approved => "Paid",
                PaymentOutcome.Rejected => "PaymentFailed",
                PaymentOutcome.Unknown => "PaymentUnknown",
                _ => throw new ArgumentOutOfRangeException(nameof(outcome))
            };

            return Task.FromResult(CreateInvoice(status));
        }
    }
}