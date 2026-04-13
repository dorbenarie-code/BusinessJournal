using System.Collections.Concurrent;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;

namespace BusinessJournal.Infrastructure.Repositories;

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly ConcurrentDictionary<Guid, Customer> _customers = new();

    public void Add(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (!_customers.TryAdd(customer.Id, customer))
        {
            throw new InvalidOperationException("A customer with the same id already exists.");
        }
    }

    public Customer? FindById(Guid customerId)
    {
        return _customers.TryGetValue(customerId, out var customer)
            ? customer
            : null;
    }

    public IReadOnlyCollection<Customer> GetAll()
    {
        return _customers.Values
            .OrderBy(customer => customer.FullName)
            .ThenBy(customer => customer.Id)
            .ToList();
    }
}