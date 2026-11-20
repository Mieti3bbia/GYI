using FYI.Application;
using FYI.Managers.Database;
using GYI.src.factories;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;

var envPath = Path.Combine(Environment.CurrentDirectory, ".env");
if (File.Exists(envPath))
{
    using var envFile = File.OpenRead(envPath);
    DotNetEnv.Env.Load(envFile, new DotNetEnv.LoadOptions(clobberExistingVars: false));
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "gyi.editor";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();

string connectionString;
try
{
    connectionString = DatabaseConfiguration.CreateConnectionString(
        builder.Configuration.GetConnectionString("Postgres"), builder.Configuration["url"]);
}
catch (FormatException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

builder.Services.AddSingleton<NpgsqlDataSource>(
    _ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<IDBService, PostgresDBService>();
builder.Services.AddSingleton<DBManager>();
builder.Services.AddSingleton<ItineraryFactory>();
builder.Services.AddSingleton<SecurityManager>();
builder.Services.AddSingleton<Coordinator>();

await using var app = builder.Build();

var initializer = app.Services.GetRequiredService<DatabaseInitializer>();
if (!await initializer.InitializeAsync())
{
    return 1;
}

if (app.Environment.IsDevelopment())
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
}

await app.RunAsync();
return 0;
