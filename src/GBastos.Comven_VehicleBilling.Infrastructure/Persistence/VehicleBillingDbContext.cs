using Comven_VehicleBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using InboxMessage = Comven_VehicleBilling.Infrastructure.Inbox.InboxMessage;
using OutboxMessage = Comven_VehicleBilling.Infrastructure.Outbox.OutboxMessage;

namespace Comven_VehicleBilling.Infrastructure.Persistence;

public sealed class VehicleBillingDbContext : DbContext
{
  public VehicleBillingDbContext(
      DbContextOptions<VehicleBillingDbContext> options)
      : base(options)
  {
  }

  public DbSet<Customer> Customers => Set<Customer>();
  public DbSet<Operation> Operations => Set<Operation>();
  public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
  public DbSet<Invoice> Invoices => Set<Invoice>();
  public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
  public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
  public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyConfigurationsFromAssembly(
        typeof(VehicleBillingDbContext).Assembly);
  }
}
