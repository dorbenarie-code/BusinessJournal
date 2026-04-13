using BusinessJournal.Web.Components;
using BusinessJournal.Web.Configuration;
using BusinessJournal.Web.Features.Auth;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<ApiOptions>(
    builder.Configuration.GetSection("Api"));

builder.Services.AddScoped<AuthSession>();

builder.Services.AddHttpClient<AuthApiClient>((serviceProvider, httpClient) =>
{
    var apiOptions = serviceProvider
        .GetRequiredService<IOptions<ApiOptions>>()
        .Value;

    if (string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    {
        throw new InvalidOperationException("Api:BaseUrl is required.");
    }

    httpClient.BaseAddress = new Uri(apiOptions.BaseUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();