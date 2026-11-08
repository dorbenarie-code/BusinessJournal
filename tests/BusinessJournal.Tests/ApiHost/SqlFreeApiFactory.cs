using BusinessJournal.Application.Contracts.Appointments;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Domain.ValueObjects;
using BusinessJournal.Infrastructure.Data.SqlServer;
using BusinessJournal.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BusinessJournal.Tests.ApiHost;

/// <summary>
/// In-process API host that cannot reach SQL Server.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Environment "Testing": user-secrets are not loaded, so no real connection
/// string is read, and the development admin is not seeded.</item>
/// <item>The SQL repositories and the connection factory are removed from DI and
/// replaced with in-memory fakes.</item>
/// <item>The connection string below only satisfies startup validation; nothing uses it.</item>
/// </list>
/// </remarks>
public sealed class SqlFreeApiFactory : WebApplicationFactory<Program>
{
    public const string SensitiveDetailMarker = "SENSITIVE-INTERNAL-DETAIL-7f3c";
    public const string InternalParameterName = "internalGuardParameter";

    private const string UnusedConnectionString = "Server=unused.invalid;Database=Unused";

    private readonly string _signingKey;

    public SqlFreeApiFactory()
        : this(TestJwtSigningKey.Value)
    {
    }

    // Internal: an xUnit class fixture may expose only one public constructor.
    internal SqlFreeApiFactory(string signingKey)
    {
        _signingKey = signingKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("SqlServer:ConnectionString", UnusedConnectionString);
        builder.UseSetting("Jwt:SigningKey", _signingKey);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<SqlServerConnectionFactory>();
            services.RemoveAll<ICustomerRepository>();
            services.RemoveAll<IAppointmentRepository>();

            services.AddSingleton<ICustomerRepository, FailingCustomerRepository>();
            services.AddSingleton<IAppointmentRepository, EmptyAppointmentRepository>();
        });
    }

    /// <summary>
    /// Simulates infrastructure failures whose messages must never reach API clients.
    /// </summary>
    private sealed class FailingCustomerRepository : ICustomerRepository
    {
        public void Add(Customer customer)
        {
            throw new InvalidOperationException(SensitiveDetailMarker);
        }

        public Customer? FindById(Guid customerId)
        {
            // Same shape as an ArgumentNullException.ThrowIfNull guard failing inside the server.
            throw new ArgumentNullException(InternalParameterName, SensitiveDetailMarker);
        }

        public IReadOnlyCollection<Customer> GetAll()
        {
            throw new InvalidOperationException(SensitiveDetailMarker);
        }
    }

    /// <summary>
    /// Contains no appointments, so lookups produce the normal NotFoundException path.
    /// </summary>
    private sealed class EmptyAppointmentRepository : IAppointmentRepository
    {
        public bool TryAdd(Appointment appointment)
        {
            throw new NotSupportedException();
        }

        public RescheduleAppointmentResult TryReschedule(Guid appointmentId, TimeRange newTime)
        {
            return RescheduleAppointmentResult.NotFound;
        }

        public bool TryCancel(Guid appointmentId)
        {
            return false;
        }

        public Appointment? FindById(Guid appointmentId)
        {
            return null;
        }

        public IReadOnlyCollection<Appointment> GetByCustomerId(Guid customerId)
        {
            return Array.Empty<Appointment>();
        }

        public IReadOnlyCollection<Appointment> GetOverlapping(DateTime startsAt, DateTime endsAt)
        {
            return Array.Empty<Appointment>();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiHostCollection
{
    public const string Name = "SQL-free API host tests";
}
