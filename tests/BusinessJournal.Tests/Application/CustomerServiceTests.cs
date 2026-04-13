using BusinessJournal.Application.Services;
using BusinessJournal.Infrastructure.Repositories;
using Xunit;

namespace BusinessJournal.Tests.Application;

public sealed class CustomerServiceTests
{
    [Fact]
    public void RegisterCustomer_WithValidValues_ShouldCreateAndStoreCustomer()
    {
        var repository = new InMemoryCustomerRepository();
        var service = new CustomerService(repository);

        var customer = service.RegisterCustomer("Michal Levi", "0501111111", "Michal@Gmail.com");

        Assert.NotNull(customer);

        var storedCustomer = repository.FindById(customer.Id);

        Assert.NotNull(storedCustomer);
        Assert.Equal("Michal Levi", storedCustomer!.FullName);
        Assert.Equal("0501111111", storedCustomer.PhoneNumber);
        Assert.Equal("michal@gmail.com", storedCustomer.Email);
    }

    [Fact]
    public void FindCustomerById_WhenCustomerExists_ShouldReturnCustomer()
    {
        var repository = new InMemoryCustomerRepository();
        var service = new CustomerService(repository);

        var createdCustomer = service.RegisterCustomer("Noa Cohen", "0502222222");

        var foundCustomer = service.FindCustomerById(createdCustomer.Id);

        Assert.NotNull(foundCustomer);
        Assert.Equal(createdCustomer.Id, foundCustomer!.Id);
    }

    [Fact]
    public void FindCustomerById_WhenCustomerDoesNotExist_ShouldReturnNull()
    {
        var repository = new InMemoryCustomerRepository();
        var service = new CustomerService(repository);

        var customer = service.FindCustomerById(Guid.NewGuid());

        Assert.Null(customer);
    }

    [Fact]
    public void GetAllCustomers_WhenCustomersExist_ShouldReturnAllCustomers()
    {
        var repository = new InMemoryCustomerRepository();
        var service = new CustomerService(repository);

        service.RegisterCustomer("Dana Levi", "0503333333");
        service.RegisterCustomer("Shani Cohen", "0504444444");

        var customers = service.GetAllCustomers();

        Assert.Equal(2, customers.Count);
    }

    [Fact]
    public void RegisterCustomer_WithInvalidFullName_ShouldThrowArgumentException()
    {
        var repository = new InMemoryCustomerRepository();
        var service = new CustomerService(repository);

        Assert.Throws<ArgumentException>(() =>
            service.RegisterCustomer("   ", "0505555555"));
    }
}