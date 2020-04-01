using AspNetCoreAzureTemplates.Configuration;
using AspNetCoreAzureTemplates.Identity;
using AspNetCoreAzureTemplates.MicrosoftGraph;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCoreAzureTemplates.Extensions
{
    public static class ConfigurationExtensions
    {
        public static IServiceCollection ConfigureAuthorization(this IServiceCollection services, IConfiguration configuration)
        {
            var authSettings = configuration.GetSection("AzureAd").Get<AzureAdOptions>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    "RequireReaderRole",
                     policy => policy.RequireAuthenticatedUser().RequireRole(authSettings.Roles.Reader, authSettings.Roles.Writer)
                );
                options.AddPolicy(
                    "RequireWriterRole",
                     policy => policy.RequireAuthenticatedUser().RequireRole(authSettings.Roles.Writer)
                );
            });

            return services;
        }

        public static IServiceCollection ConfigureSignalR(
            this IServiceCollection services,
            IWebHostEnvironment env,
            IConfiguration configuration
        )
        {
            if (env.IsDevelopment())
            {
                services.AddSignalR().AddNewtonsoftJsonProtocol();
            }
            else
            {
                string azureSignalrConnectionString = configuration["Azure:SignalR:ConnectionString"];

                services.AddSignalR().AddNewtonsoftJsonProtocol().AddAzureSignalR(options =>
                {
                    options.ConnectionString = azureSignalrConnectionString;
                });
            }

            return services;
        }

        public static IServiceCollection ConfigureLogging(this IServiceCollection services, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                services.AddLogging(config =>
                {
                    config.AddDebug();
                    config.AddConsole();
                });
            }
            else
            {
                services.AddApplicationInsightsTelemetry();

                services.AddLogging(config =>
                {
                    config.AddApplicationInsights();
                });
            }

            return services;
        }

        public static IServiceCollection ConfigureSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc(
                    "v1",
                    new OpenApiInfo
                    {
                        Title = "AspNetCoreAzureTemplates API",
                        Version = "v1"
                    }
                );

                var bearerScheme = new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    },
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                };
                c.AddSecurityDefinition("Bearer", bearerScheme);

                var requirement = new OpenApiSecurityRequirement
                {
                    { bearerScheme, Enumerable.Empty<string>().ToList() }
                };
                c.AddSecurityRequirement(requirement);

                var filePath = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "AspNetCoreAzureTemplates.xml");
                c.IncludeXmlComments(filePath);
            });

            return services;
        }

        public static IServiceCollection ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var authSettings = configuration.GetSection("AzureAd").Get<AzureAdOptions>();

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
                            if (context.Request.Path.StartsWithSegments("/hub"))
                            {
                                string? accessToken = context.Request.Query["access_token"];

                                if (!string.IsNullOrEmpty(accessToken))
                                {
                                    context.Token = accessToken;
                                }
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }

        public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var authSettings = configuration.GetSection("AzureAd").Get<AzureAdOptions>();
            var healthCheckConfig = configuration.GetSection("HealthCheck");

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

            return services;
        }

        public static IServiceCollection ConfigureInjection(this IServiceCollection services, IConfiguration configuration)
        {
            // Register app options
            services.Configure<AzureAdOptions>(configuration.GetSection("AzureAd"));

            // Register technical services
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IIdentityService, AzureAdIdentityService>();
            services.AddScoped<IGraphServiceClient, GraphServiceClient>();
            services.AddScoped<IAuthenticationProvider, OnBehalfOfMsGraphAuthenticationProvider>();
            services.AddScoped<IGraphApiService, GraphApiService>();

            // TODO : Register business services

            return services;
        }
    }
}
