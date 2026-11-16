using System.Text.Json.Serialization;
using CheckApps.Application.DependencyInjection;
using CheckApps.Infrastructure.Configuration;
using CheckApps.Infrastructure.DependencyInjection;
using CheckApps.Infrastructure.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Modo demonstração (spec 023, ADR-013): no modo demo, banco e fontes de demonstração vencem qualquer outra configuração.
builder.Configuration.ApplyCheckAppsMode();

const string AngularDevCorsPolicy = "AngularDev";

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddCheckAppsInfrastructure(builder.Configuration);
builder.Services.AddCheckAppsApplication();

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();
}

app.UseHttpsRedirection();
app.UseCors(AngularDevCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();
