using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;

namespace BusinessJournal.Application.Services;

public sealed class CustomerService
{
    private readonly ICustomerRepository _customerRepository;

    public CustomerService(ICustomerRepository customerRepository)
    {
        ArgumentNullException.ThrowIfNull(customerRepository);
        _customerRepository = customerRepository;
    }

    public Customer RegisterCustomer(string fullName, string phoneNumber, string? email = null)
    {
        var customer = Customer.Create(fullName, phoneNumber, email);

        _customerRepository.Add(customer);

        return customer;
    }

    public Customer? FindCustomerById(Guid customerId)
    {
        return _customerRepository.FindById(customerId);
    }

    public IReadOnlyCollection<Customer> GetAllCustomers()
    {
        return _customerRepository.GetAll();
    }
}