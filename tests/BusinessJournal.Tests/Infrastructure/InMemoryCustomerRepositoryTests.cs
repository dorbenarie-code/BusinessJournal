using BusinessJournal.Domain.Entities;
using BusinessJournal.Infrastructure.Repositories;
using Xunit;

namespace BusinessJournal.Tests.Infrastructure;

public sealed class InMemoryCustomerRepositoryTests
{
    [Fact]
    public void Add_WithValidCustomer_ShouldStoreCustomer()
    {
        var repository = new InMemoryCustomerRepository();

        var customer = Customer.Create(
            "Rachel Cohen",
            "0501234567",
            "Rachel@Gmail.com");

        repository.Add(customer);

        var storedCustomer = repository.FindById(customer.Id);

        Assert.NotNull(storedCustomer);
        Assert.Equal(customer.Id, storedCustomer!.Id);
        Assert.Equal("Rachel Cohen", storedCustomer.FullName);
        Assert.Equal("0501234567", storedCustomer.PhoneNumber);
        Assert.Equal("rachel@gmail.com", storedCustomer.Email);
    }

    [Fact]
    public void Add_WithCustomerWithoutEmail_ShouldStoreNullEmail()
    {
        var repository = new InMemoryCustomerRepository();

        var customer = Customer.Create(
            "Michal Levi",
            "0521234567");

        repository.Add(customer);

        var storedCustomer = repository.FindById(customer.Id);

        Assert.NotNull(storedCustomer);
        Assert.Null(storedCustomer!.Email);
    }

    [Fact]
    public void FindById_WhenCustomerDoesNotExist_ShouldReturnNull()
    {
        var repository = new InMemoryCustomerRepository();

        var customer = repository.FindById(Guid.NewGuid());

        Assert.Null(customer);
    }

    [Fact]
    public void GetAll_WhenCustomersExist_ShouldReturnAllCustomersInFullNameThenIdOrder()
    {
        var repository = new InMemoryCustomerRepository();

        var secondCustomer = Customer.Create(
            "Shani Cohen",
            "0502222222",
            "shani@gmail.com");

        var firstCustomer = Customer.Create(
            "Dana Levi",
            "0501111111",
            "dana@gmail.com");

        repository.Add(secondCustomer);
        repository.Add(firstCustomer);

        var customers = repository.GetAll().ToList();

        Assert.Equal(2, customers.Count);

        Assert.Equal(firstCustomer.Id, customers[0].Id);
        Assert.Equal("Dana Levi", customers[0].FullName);

        Assert.Equal(secondCustomer.Id, customers[1].Id);
        Assert.Equal("Shani Cohen", customers[1].FullName);
    }

    [Fact]
    public void GetAll_WhenNoCustomersExist_ShouldReturnEmptyCollection()
    {
        var repository = new InMemoryCustomerRepository();

        var customers = repository.GetAll();

        Assert.Empty(customers);
    }

    [Fact]
    public void Add_WhenSameCustomerIsAddedTwice_ShouldThrowInvalidOperationException()
    {
        var repository = new InMemoryCustomerRepository();

        var customer = Customer.Create(
            "Rachel Cohen",
            "0501234567",
            "rachel@gmail.com");

        repository.Add(customer);

        Assert.Throws<InvalidOperationException>(() => repository.Add(customer));
    }
}