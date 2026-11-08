using BusinessJournal.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BusinessJournal.Tests.Api;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // appsettings.json no longer contains a signing key.
        builder.UseSetting("Jwt:SigningKey", TestJwtSigningKey.Value);

        // The Development environment loads user-secrets, which may point at a real
        // database. Force the API under test onto the verified disposable database.
        builder.UseSetting("SqlServer:ConnectionString", DisposableTestDatabase.ConnectionString);
    }
}