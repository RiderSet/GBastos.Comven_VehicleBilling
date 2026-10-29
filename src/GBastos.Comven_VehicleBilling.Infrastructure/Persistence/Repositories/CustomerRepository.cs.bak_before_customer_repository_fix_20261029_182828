using Comven_VehicleBilling.Application.Abstractions.Persistence;
using Comven_VehicleBilling.Application.Customers;
using Comven_VehicleBilling.Domain.Entities;
using Comven_VehicleBilling.Domain.Enums;
using Comven_VehicleBilling.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Comven_VehicleBilling.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly VehicleBillingDbContext _dbContext;

    public CustomerRepository(VehicleBillingDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<CustomerDto> CreateAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var customer = new Customer(
            Guid.NewGuid(),
            command.Name,
            CustomerPlan.Prepaid,
            Money.Zero,
            DateTimeOffset.UtcNow);

        await _dbContext.Customers.AddAsync(
            customer,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerDto(
            customer.Id,
            customer.Name,
            command.Document.Trim(),
            command.Email.Trim());
    }

    public async Task<CustomerDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (customer is null)
            return null;

        return new CustomerDto(
            customer.Id,
            customer.Name,
            string.Empty,
            string.Empty);
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var customers = await _dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Name)
            .ToListAsync(cancellationToken);

        return customers
            .Select(customer => new CustomerDto(
                customer.Id,
                customer.Name,
                string.Empty,
                string.Empty))
            .ToList();
    }
}
