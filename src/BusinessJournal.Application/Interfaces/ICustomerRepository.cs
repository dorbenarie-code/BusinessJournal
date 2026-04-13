using BusinessJournal.Domain.Entities;

namespace BusinessJournal.Application.Interfaces;

public interface ICustomerRepository
{
    void Add(Customer customer);
    Customer? FindById(Guid customerId);
    IReadOnlyCollection<Customer> GetAll();
}