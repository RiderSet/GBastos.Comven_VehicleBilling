using Comven_VehicleBilling.Application.Abstractions.Payments;
using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Invoices;

namespace Comven_VehicleBilling.UnitTests.Invoices;

public sealed class InvoiceServiceTests
{
    private readonly FakeInvoiceRepository _repository;
    private readonly InvoiceService _service;

    public InvoiceServiceTests()
    {
        _repository = new FakeInvoiceRepository();
        _service = new InvoiceService(_repository);
    }

    [Fact]
    public async Task CreateAsync_ShouldDelegateToRepository()
    {
        var command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            new DateOnly(2026, 10, 1));

        var result = await _service.CreateAsync(command);

        Assert.Equal(_repository.Invoice.Id, result.Id);
        Assert.Same(command, _repository.LastCreateCommand);
        Assert.Equal(1, _repository.CreateCalls);
    }

    [Fact]
    public async Task CreateSupplementaryAsync_ShouldDelegateToRepository()
    {
        var command = new CreateSupplementaryInvoiceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "supplementary-001");

        var result = await _service.CreateSupplementaryAsync(command);

        Assert.Equal(_repository.Invoice.Id, result.Id);
        Assert.Same(command, _repository.LastSupplementaryCommand);
        Assert.Equal(1, _repository.SupplementaryCalls);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnInvoice()
    {
        var result = await _service.GetByIdAsync(_repository.Invoice.Id);

        Assert.NotNull(result);
        Assert.Equal(_repository.Invoice.Id, result.Id);
        Assert.Equal(1, _repository.GetByIdCalls);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldRejectEmptyId()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetByIdAsync(Guid.Empty));

        Assert.Equal(0, _repository.GetByIdCalls);
    }

    [Fact]
    public async Task GetAllAsync_ShouldDelegateToRepository()
    {
        var customerId = Guid.NewGuid();

        var result = await _service.GetAllAsync(customerId);

        Assert.NotNull(result);
        Assert.Equal(customerId, _repository.LastCustomerId);
        Assert.Equal(1, _repository.GetAllCalls);
    }

    [Fact]
    public async Task GetAllAsync_ShouldRejectEmptyCustomerId()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetAllAsync(Guid.Empty));

        Assert.Equal(0, _repository.GetAllCalls);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectEmptyCustomerId()
    {
        var command = new CreateInvoiceCommand(
            Guid.Empty,
            new DateOnly(2026, 10, 1));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(command));

        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectInvalidReferenceDate()
    {
        var command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            DateOnly.MinValue);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(command));

        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task CreateSupplementaryAsync_ShouldRejectEmptyCustomerId()
    {
        var command = new CreateSupplementaryInvoiceCommand(
            Guid.Empty,
            Guid.NewGuid(),
            "key-001");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateSupplementaryAsync(command));

        Assert.Equal(0, _repository.SupplementaryCalls);
    }

    [Fact]
    public async Task CreateSupplementaryAsync_ShouldRejectEmptyOriginalInvoiceId()
    {
        var command = new CreateSupplementaryInvoiceCommand(
            Guid.NewGuid(),
            Guid.Empty,
            "key-001");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateSupplementaryAsync(command));

        Assert.Equal(0, _repository.SupplementaryCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateSupplementaryAsync_ShouldRejectEmptyIdempotencyKey(
        string? key)
    {
        var command = new CreateSupplementaryInvoiceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            key!);

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _service.CreateSupplementaryAsync(command));

        Assert.Equal(0, _repository.SupplementaryCalls);
    }

    [Fact]
    public async Task CreateSupplementaryAsync_ShouldRejectKeyLongerThan200Characters()
    {
        var command = new CreateSupplementaryInvoiceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('A', 201));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => _service.CreateSupplementaryAsync(command));

        Assert.Equal(0, _repository.SupplementaryCalls);
    }

    [Fact]
    public async Task CreateSupplementaryAsync_ShouldAcceptKeyWith200Characters()
    {
        var command = new CreateSupplementaryInvoiceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('A', 200));

        await _service.CreateSupplementaryAsync(command);

        Assert.Equal(1, _repository.SupplementaryCalls);
    }

    [Fact]
    public async Task CreateAsync_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            new DateOnly(2026, 10, 1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.CreateAsync(command, cancellation.Token));

        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task CreateSupplementaryAsync_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var command = new CreateSupplementaryInvoiceCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "key-001");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.CreateSupplementaryAsync(
                command,
                cancellation.Token));

        Assert.Equal(0, _repository.SupplementaryCalls);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.GetByIdAsync(
                Guid.NewGuid(),
                cancellation.Token));

        Assert.Equal(0, _repository.GetByIdCalls);
    }


    [Fact]
    public async Task GetAllAsync_ShouldPropagateCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.GetAllAsync(
                null,
                cancellation.Token));

        Assert.Equal(0, _repository.GetAllCalls);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullWhenInvoiceDoesNotExist()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal(1, _repository.GetByIdCalls);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectNullCommand()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateAsync(null!));

        Assert.Equal(0, _repository.CreateCalls);
    }

    [Fact]
    public async Task CreateSupplementaryAsync_ShouldRejectNullCommand()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateSupplementaryAsync(null!));

        Assert.Equal(0, _repository.SupplementaryCalls);
    }

    [Fact]
    public async Task Methods_ShouldForwardCancellationTokenToRepository()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        await _service.CreateAsync(
            new CreateInvoiceCommand(
                Guid.NewGuid(),
                new DateOnly(2026, 10, 1)),
            token);

        await _service.CreateSupplementaryAsync(
            new CreateSupplementaryInvoiceCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "forward-token-001"),
            token);

        await _service.GetByIdAsync(Guid.NewGuid(), token);

        await _service.GetAllAsync(Guid.NewGuid(), token);

        Assert.Equal(token, _repository.LastCreateCancellationToken);
        Assert.Equal(token, _repository.LastSupplementaryCancellationToken);
        Assert.Equal(token, _repository.LastGetByIdCancellationToken);
        Assert.Equal(token, _repository.LastGetAllCancellationToken);
    }
    private sealed class FakeInvoiceRepository : IInvoiceRepository
    {
        public InvoiceDto Invoice { get; } = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            12.50m,
            "PaymentPending",
            DateTimeOffset.UtcNow,
            null,
            Array.Empty<InvoiceItemDto>());

        public CreateInvoiceCommand? LastCreateCommand { get; private set; }

        public CreateSupplementaryInvoiceCommand? LastSupplementaryCommand { get; private set; }

        public Guid? LastCustomerId { get; private set; }

        public CancellationToken LastCreateCancellationToken { get; private set; }

        public CancellationToken LastSupplementaryCancellationToken { get; private set; }

        public CancellationToken LastGetByIdCancellationToken { get; private set; }

        public CancellationToken LastGetAllCancellationToken { get; private set; }
        public int CreateCalls { get; private set; }

        public int SupplementaryCalls { get; private set; }

        public int GetByIdCalls { get; private set; }

        public int GetAllCalls { get; private set; }

        public Task<InvoiceDto> CreateAsync(
            CreateInvoiceCommand command,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastCreateCancellationToken = cancellationToken;
            LastCreateCommand = command;
            return Task.FromResult(Invoice);
        }

        public Task<InvoiceDto> CreateSupplementaryAsync(
            CreateSupplementaryInvoiceCommand command,
            CancellationToken cancellationToken = default)
        {
            SupplementaryCalls++;
            LastSupplementaryCancellationToken = cancellationToken;
            LastSupplementaryCommand = command;
            return Task.FromResult(Invoice);
        }

        public Task<InvoiceDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            LastGetByIdCancellationToken = cancellationToken;
            return Task.FromResult<InvoiceDto?>(
                id == Invoice.Id ? Invoice : null);
        }

        public Task<IReadOnlyList<InvoiceDto>> GetAllAsync(
            Guid? customerId = null,
            CancellationToken cancellationToken = default)
        {
            GetAllCalls++;
            LastGetAllCancellationToken = cancellationToken;
            LastCustomerId = customerId;

            IReadOnlyList<InvoiceDto> invoices = new[] { Invoice };

            return Task.FromResult(invoices);
        }
        public Task<InvoiceDto?> StartPaymentAsync(
            Guid invoiceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<InvoiceDto?>(
                invoiceId == Invoice.Id ? Invoice : null);
        }

        public Task<InvoiceDto> CompletePaymentAsync(
            Guid invoiceId,
            PaymentOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Invoice);
        }
    }
}

