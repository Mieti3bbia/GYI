using FYI.Managers.Database;
using Npgsql;

using (var envFile = File.OpenRead(Path.Combine(Environment.CurrentDirectory, ".env")))
{
    DotNetEnv.Env.Load(envFile, new DotNetEnv.LoadOptions(clobberExistingVars: false));
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

string connectionString;
try
{
    connectionString = DatabaseConfiguration.CreateConnectionString(
        Environment.GetEnvironmentVariable("url"));
}
catch (FormatException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

builder.Services.AddSingleton<NpgsqlDataSource>(
    _ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<DatabaseInitializer>();

await using var app = builder.Build();

var initializer = app.Services.GetRequiredService<DatabaseInitializer>();
if (!await initializer.InitializeAsync())
{
    return 1;
}

app.MapControllers();

await app.RunAsync();
return 0;
