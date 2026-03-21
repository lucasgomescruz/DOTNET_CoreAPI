
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Project.Infrastructure.Data;
using Project.WebApi.Configurations;
using Project.WebApi.Infrastructure;

static async Task InitialiseDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

var builder = WebApplication.CreateBuilder(args);

// Remove the "Server" header at the Kestrel level
builder.WebHost.ConfigureKestrel(opt => opt.AddServerHeader = false);

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddWebServices(builder.Configuration);
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await InitialiseDatabaseAsync(app);

}
else
{
    app.UseHsts();
}

// Security headers — must be added before any response-producing middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.UseHealthChecks("/health");

app.UseHttpsRedirection();

app.UseCors("DefaultCors");

app.UseRateLimiter();

app.UseRouting();
// Enable Swagger UI before authentication/authorization so it's accessible
app.UseSwaggerConfiguration();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.UseExceptionHandler(options => { });

app.Map("/", () => Results.Redirect("/swagger"));

app.Run();

public partial class Program { }

