using System.Text.Json.Serialization;
using ITTicketing.Api.Data;
using ITTicketing.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<TicketDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<TicketStore>();
builder.Services.AddScoped<TicketEventOrchestrator>();
builder.Services.AddScoped<EscalationEngine>();
builder.Services.AddScoped<SlaEvaluator>();
builder.Services.AddSingleton<INotificationChannelSender, NotificationChannelSender>();
builder.Services.AddHttpClient(nameof(NotificationChannelSender));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.Configure<SlaMonitoringOptions>(builder.Configuration.GetSection(SlaMonitoringOptions.SectionName));
builder.Services.Configure<ApiKeyAuthOptions>(builder.Configuration.GetSection(ApiKeyAuthOptions.SectionName));
builder.Services.AddHostedService<SlaMonitoringService>();

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
        policy.RequireAuthenticatedUser().RequireRole(ApiKeyAuthenticationHandler.AdminRole));
    options.AddPolicy("Compliance", policy =>
        policy.RequireAuthenticatedUser().RequireRole(ApiKeyAuthenticationHandler.ComplianceRole));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

