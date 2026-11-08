using BusinessJournal.Domain.Entities;
using BusinessJournal.Infrastructure.Data.SqlServer;
using BusinessJournal.Infrastructure.Repositories;
using BusinessJournal.Tests.TestSupport;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace BusinessJournal.Tests.Infrastructure;

[Collection("SqlServer integration tests")]
public sealed class SqlCustomerRepositoryTests
{
    private readonly string _connectionString;
    private readonly SqlCustomerRepository _customerRepository;

    public SqlCustomerRepositoryTests()
    {
        // CleanupDatabase deletes all rows, so only the verified disposable database is allowed.
        _connectionString = DisposableTestDatabase.ConnectionString;

        var factory = new SqlServerConnectionFactory(
            Options.Create(new SqlServerOptions
            {
                ConnectionString = _connectionString
            }));

        _customerRepository = new SqlCustomerRepository(factory);

        CleanupDatabase();
    }

    [Fact]
    public void Add_WithValidCustomer_ShouldStoreCustomer()
    {
        var customer = Customer.Create(
            "Rachel Cohen",
            "0501234567",
            "Rachel@Gmail.com");

        _customerRepository.Add(customer);

        var storedCustomer = _customerRepository.FindById(customer.Id);

        Assert.NotNull(storedCustomer);
        Assert.Equal(customer.Id, storedCustomer!.Id);
        Assert.Equal("Rachel Cohen", storedCustomer.FullName);
        Assert.Equal("0501234567", storedCustomer.PhoneNumber);
        Assert.Equal("rachel@gmail.com", storedCustomer.Email);
    }

    [Fact]
    public void Add_WithCustomerWithoutEmail_ShouldStoreNullEmail()
    {
        var customer = Customer.Create(
            "Michal Levi",
            "0521234567");

        _customerRepository.Add(customer);

        var storedCustomer = _customerRepository.FindById(customer.Id);

        Assert.NotNull(storedCustomer);
        Assert.Null(storedCustomer!.Email);
    }

    [Fact]
    public void FindById_WhenCustomerDoesNotExist_ShouldReturnNull()
    {
        var customer = _customerRepository.FindById(Guid.NewGuid());

        Assert.Null(customer);
    }

    [Fact]
    public void GetAll_WhenCustomersExist_ShouldReturnAllCustomersInFullNameThenIdOrder()
    {
        var secondCustomer = Customer.Create(
            "Shani Cohen",
            "0502222222",
            "shani@gmail.com");

        var firstCustomer = Customer.Create(
            "Dana Levi",
            "0501111111",
            "dana@gmail.com");

        _customerRepository.Add(secondCustomer);
        _customerRepository.Add(firstCustomer);

        var customers = _customerRepository.GetAll().ToList();

        Assert.Equal(2, customers.Count);

        Assert.Equal(firstCustomer.Id, customers[0].Id);
        Assert.Equal("Dana Levi", customers[0].FullName);

        Assert.Equal(secondCustomer.Id, customers[1].Id);
        Assert.Equal("Shani Cohen", customers[1].FullName);
    }

    [Fact]
    public void GetAll_WhenNoCustomersExist_ShouldReturnEmptyCollection()
    {
        var customers = _customerRepository.GetAll();

        Assert.Empty(customers);
    }

    private void CleanupDatabase()
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM dbo.Appointments;
            DELETE FROM dbo.Customers;
            """;

        command.ExecuteNonQuery();
    }
}