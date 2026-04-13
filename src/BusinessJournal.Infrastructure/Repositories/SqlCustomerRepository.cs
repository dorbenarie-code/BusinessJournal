using System.Data;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Infrastructure.Data.SqlServer;
using Microsoft.Data.SqlClient;

namespace BusinessJournal.Infrastructure.Repositories;

public sealed class SqlCustomerRepository : ICustomerRepository
{
    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlCustomerRepository(SqlServerConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public void Add(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO dbo.Customers (Id, FullName, PhoneNumber, Email)
            VALUES (@Id, @FullName, @PhoneNumber, @Email);
            """;

        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier)
        {
            Value = customer.Id
        });

        command.Parameters.Add(new SqlParameter("@FullName", SqlDbType.NVarChar, 200)
        {
            Value = customer.FullName
        });

        command.Parameters.Add(new SqlParameter("@PhoneNumber", SqlDbType.NVarChar, 50)
        {
            Value = customer.PhoneNumber
        });

        command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 320)
        {
            Value = customer.Email is null ? DBNull.Value : customer.Email
        });

        command.ExecuteNonQuery();
    }

    public Customer? FindById(Guid customerId)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, FullName, PhoneNumber, Email
            FROM dbo.Customers
            WHERE Id = @Id;
            """;

        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.UniqueIdentifier)
        {
            Value = customerId
        });

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return MapCustomer(reader);
    }

    public IReadOnlyCollection<Customer> GetAll()
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, FullName, PhoneNumber, Email
            FROM dbo.Customers
            ORDER BY FullName, Id;
            """;

        using var reader = command.ExecuteReader();

        var customers = new List<Customer>();

        while (reader.Read())
        {
            customers.Add(MapCustomer(reader));
        }

        return customers;
    }

    private static Customer MapCustomer(SqlDataReader reader)
    {
        var id = reader.GetGuid(reader.GetOrdinal("Id"));
        var fullName = reader.GetString(reader.GetOrdinal("FullName"));
        var phoneNumber = reader.GetString(reader.GetOrdinal("PhoneNumber"));

        string? email = reader.IsDBNull(reader.GetOrdinal("Email"))
            ? null
            : reader.GetString(reader.GetOrdinal("Email"));

        return Customer.Restore(id, fullName, phoneNumber, email);
    }
}