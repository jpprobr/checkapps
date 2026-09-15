using System.Text.Json.Serialization;
using CheckApps.Application.DependencyInjection;
using CheckApps.Infrastructure.DependencyInjection;
using CheckApps.Infrastructure.DynamoDb;

var builder = WebApplication.CreateBuilder(args);

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
    await scope.ServiceProvider.GetRequiredService<DynamoDbTableBootstrapper>().EnsureTableExistsAsync();
}

app.UseHttpsRedirection();
app.UseCors(AngularDevCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();
