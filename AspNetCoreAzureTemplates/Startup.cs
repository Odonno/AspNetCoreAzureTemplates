using AspNetCoreAzureTemplates.Configuration;
using AspNetCoreAzureTemplates.Extensions;
using AspNetCoreAzureTemplates.Hubs;
using AspNetCoreAzureTemplates.Identity;
using AspNetCoreAzureTemplates.MicrosoftGraph;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;

namespace AspNetCoreAzureTemplates
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment CurrentEnvironment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment currentEnvironment)
        {
            Configuration = configuration;
            CurrentEnvironment = currentEnvironment;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var authSettings = Configuration.GetSection("AzureAd").Get<AzureAdOptions>();
            var healthCheckConfig = Configuration.GetSection("HealthCheck");

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddHttpClient();

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

            services.ConfigureSignalR(CurrentEnvironment, Configuration["Azure:SignalR:ConnectionString"]);
            services.ConfigureLogging(CurrentEnvironment);

            services.AddApplicationInsightsTelemetry();

            services.ConfigureSwagger();

            services.ConfigureAuthentication(authSettings);

            services.ConfigureHealthChecks(authSettings, healthCheckConfig);

            services.Configure<AzureAdOptions>(Configuration.GetSection("AzureAd"));

            // Register technical services
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IIdentityService, AzureAdIdentityService>();
            services.AddScoped<IGraphServiceClient, GraphServiceClient>();
            services.AddScoped<IAuthenticationProvider, OnBehalfOfMsGraphAuthenticationProvider>();
            services.AddScoped<IGraphApiService, GraphApiService>();

            // TODO : Register business services
        }               

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseCors(builder =>
            {
                builder.SetIsOriginAllowed(_ => true)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            app.UseAuthentication();

            if (CurrentEnvironment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                app.UseSignalR(routes =>
                {
                    routes.MapHub<ValuesHub>("/values");
                });
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();

                app.UseAzureSignalR(routes =>
                {
                    routes.MapHub<ValuesHub>("/values");
                });
            }

            app.UseHealthChecks("/healthz", new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            });
            app.UseHealthChecksUI();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.RoutePrefix = string.Empty;
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "AspNetCoreAzureTemplates API V1");
            });

            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
