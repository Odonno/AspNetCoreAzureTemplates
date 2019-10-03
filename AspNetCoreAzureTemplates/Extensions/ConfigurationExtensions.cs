using AspNetCoreAzureTemplates.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCoreAzureTemplates.Extensions
{
    public static class ConfigurationExtensions
    {
        public static void ConfigureHealthChecks(this IServiceCollection services, AzureAdOptions authSettings, IConfigurationSection healthCheckConfig)
        {
            HubConnection CreateSignalrHubConnection(string url)
            {
                return new HubConnectionBuilder()
                        .WithUrl(url, options =>
                        {
                            options.AccessTokenProvider = async () =>
                            {
                                var authContext = new AuthenticationContext(authSettings.Authority);

                                var credentials = new ClientCredential(authSettings.ClientId, authSettings.ClientSecret);
                                var result = await authContext.AcquireTokenAsync(authSettings.ClientId, credentials);
                                return result.AccessToken;
                            };
                        })
                        .Build();
            }

            services.AddHealthChecks()
                .AddUrlGroup(new Uri("https://login.microsoftonline.com"), "azure ad")
                .AddUrlGroup(new Uri("https://graph.microsoft.com"), "graph api")
                .AddSignalRHub(() => CreateSignalrHubConnection(healthCheckConfig["SignalR:Values"]), "signalr hub - values");

            services.AddHealthChecksUI();
        }

        public static void ConfigureAuthentication(this IServiceCollection services, AzureAdOptions authSettings)
        {
            services
                .AddAuthentication(sharedOptions =>
                {
                    sharedOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.Audience = authSettings.ClientId;
                    options.Authority = authSettings.Authority;

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];

                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });
        }

        public static void ConfigureSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "AspNetCoreAzureTemplates API", Version = "v1" });

                c.AddSecurityDefinition("Bearer", new ApiKeyScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = "header",
                    Type = "apiKey"
                });

                c.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>
                {
                    { "Bearer", Enumerable.Empty<string>() }
                });

                var filePath = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "AspNetCoreAzureTemplates.xml");
                c.IncludeXmlComments(filePath);
            });
        }

        public static void ConfigureLogging(this IServiceCollection services, IHostingEnvironment currentEnvironment)
        {
            if (currentEnvironment.IsDevelopment())
            {
                services.AddLogging(config =>
                {
                    config.AddDebug();
                    config.AddConsole();
                });
            }
            else
            {
                services.AddLogging(config =>
                {
                    config.AddApplicationInsights();
                });
            }
        }

        public static void ConfigureSignalR(
            this IServiceCollection services, 
            IHostingEnvironment currentEnvironment,
            string azureSignalrConnectionString
        )
        {
            if (currentEnvironment.IsDevelopment())
            {
                services.AddSignalR();
            }
            else
            {
                services.AddSignalR().AddAzureSignalR(options =>
                {
                    options.ConnectionString = azureSignalrConnectionString;
                });
            }
        }
    }
}
