using System.Text;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenShock.API.Realtime;
using OpenShock.API.Services;
using OpenShock.API.Services.Account;
using OpenShock.API.Services.Email.Mailjet;
using OpenShock.API.Services.Email.Smtp;
using OpenShock.Common;
using OpenShock.Common.Constants;
using OpenShock.Common.DataAnnotations;
using OpenShock.Common.Hubs;
using OpenShock.Common.JsonSerialization;
using OpenShock.Common.Models;
using OpenShock.Common.OpenShockDb;
using OpenShock.Common.Redis;
using OpenShock.Common.Services.Device;
using OpenShock.Common.Services.LCGNodeProvisioner;
using OpenShock.Common.Services.Ota;
using OpenShock.Common.Services.Session;
using OpenShock.Common.Services.Turnstile;
using OpenShock.Common.Utils;
using Redis.OM;
using Redis.OM.Contracts;
using Scalar.AspNetCore;
using Semver;
using Serilog;

namespace OpenShock.API;

public sealed class Startup
{

    private readonly ApiConfig _apiConfig;

    public Startup(IConfiguration configuration)
    {
        _apiConfig = configuration.GetChildren()
                         .FirstOrDefault(x => x.Key.Equals("openshock", StringComparison.InvariantCultureIgnoreCase))?
                         .Get<ApiConfig>() ??
                     throw new Exception("Couldn't bind config, check config file");

        var startupLogger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();

        MiniValidation.MiniValidator.TryValidate(_apiConfig, true, true, out var errors);
        if (errors.Count > 0)
        {
            var sb = new StringBuilder();

            foreach (var error in errors)
            {
                sb.AppendLine($"Error on field [{error.Key}] reason: {string.Join(", ", error.Value)}");
            }

            startupLogger.Error(
                "Error validating config, please fix your configuration / environment variables\nFound the following errors:\n{Errors}",
                sb.ToString());
            Environment.Exit(-10);
        }
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ApiConfig>(_apiConfig);

        var commonServices = services.AddOpenShockServices(_apiConfig);
        
        services.AddSingleton<ILCGNodeProvisioner, LCGNodeProvisioner>();
        
        services.AddSingleton(x =>
        {
            var config = x.GetRequiredService<ApiConfig>();
            return new CloudflareTurnstileOptions
            {
                SecretKey = config.Turnstile.SecretKey ?? string.Empty,
                SiteKey = config.Turnstile.SiteKey ?? string.Empty
            };
        });
        services.AddHttpClient<ICloudflareTurnstileService, CloudflareTurnstileService>();
        
        // ----------------- MAIL SETUP -----------------
        var emailConfig = _apiConfig.Mail;
        switch (emailConfig.Type)
        {
            case ApiConfig.MailConfig.MailType.Mailjet:
                if (emailConfig.Mailjet == null)
                    throw new Exception("Mailjet config is null but mailjet is selected as mail type");
                services.AddMailjetEmailService(emailConfig.Mailjet, emailConfig.Sender);
                break;
            case ApiConfig.MailConfig.MailType.Smtp:
                if (emailConfig.Smtp == null)
                    throw new Exception("SMTP config is null but SMTP is selected as mail type");
                services.AddSmtpEmailService(emailConfig.Smtp, emailConfig.Sender, new SmtpServiceTemplates
                {
                    PasswordReset = SmtpTemplate.ParseFromFileThrow("SmtpTemplates/PasswordReset.liquid").Result,
                    EmailVerification = SmtpTemplate.ParseFromFileThrow("SmtpTemplates/EmailVerification.liquid").Result
                });
                break;
            default:
                throw new Exception("Unknown mail type");
        }
        
        services.AddSignalR()
            .AddOpenShockStackExchangeRedis(options => { options.Configuration = commonServices.RedisConfig; })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                options.PayloadSerializerOptions.Converters.Add(new SemVersionJsonConverter());
            });

        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IDeviceUpdateService, DeviceUpdateService>();
        services.AddScoped<IOtaService, OtaService>();
        services.AddScoped<IAccountService, AccountService>();

        services.AddOpenApi();
        
        //services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        
        services.AddHostedService<RedisSubscriberService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory,
        ILogger<Startup> logger)
    {
        ApplicationLogging.LoggerFactory = loggerFactory;

        app.UseCommonOpenShockServices();
        
        if (!_apiConfig.Db.SkipMigration)
        {
            logger.LogInformation("Running database migrations...");
            using var scope = app.ApplicationServices.CreateScope();
            var openShockContext = scope.ServiceProvider.GetRequiredService<OpenShockContext>();
            var pendingMigrations = openShockContext.Database.GetPendingMigrations().ToList();

            if (pendingMigrations.Count > 0)
            {
                logger.LogInformation("Found pending migrations, applying [{@Migrations}]", pendingMigrations);
                openShockContext.Database.Migrate();
                logger.LogInformation("Applied database migrations... proceeding with startup");
            }
            else logger.LogInformation("No pending migrations found, proceeding with startup");
        }
        else logger.LogWarning("Skipping possible database migrations...");

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapOpenApi();
            
            endpoints.MapControllers();
            endpoints.MapHub<UserHub>("/1/hubs/user",
                options => { options.Transports = HttpTransportType.WebSockets; });
            endpoints.MapHub<ShareLinkHub>("/1/hubs/share/link/{id}",
                options => { options.Transports = HttpTransportType.WebSockets; });

            endpoints.MapScalarApiReference(options =>
            {
                options.OpenApiRoutePattern = "/swagger/{documentName}/swagger.json";
            });
        });
    }
}