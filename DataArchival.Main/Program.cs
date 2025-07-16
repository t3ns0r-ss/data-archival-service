using System.Text;
using DataArchival.Core.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using DataArchival.Core.Services;
using DataArchival.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataArchival.Main;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        
        var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
                };
            });
        
        builder.Services.AddSingleton<IConnectionService, ConnectionService>();
        builder.Services.AddScoped<IArchiveConfigService, ArchiveConfigService>();
        builder.Services.AddScoped<ISchemaDiscoveryService, SchemaDiscoveryService>();
        builder.Services.AddScoped<IDataArchivalService, DataArchivalService>();
        builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
        builder.Services.AddScoped<IArchiveDataService, ArchiveDataService>();
        builder.Services.AddHostedService<ArchivalScheduler>();

        builder.Services.AddAuthorization();
        
        builder.Services.AddLogging(logger =>
        {
            logger.AddConsole();
            logger.AddDebug();
        });
        
        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        //app.MapHealthChecks("/health");

        using (var scope = app.Services.CreateScope())
        {
            var connectionService = scope.ServiceProvider.GetRequiredService<IConnectionService>();
            await connectionService.InitializeArchiveDatabaseAsync();
        }

        await app.RunAsync();
    }
}