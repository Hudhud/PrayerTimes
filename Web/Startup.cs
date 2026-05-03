using Application.Interfaces;
using Application.Mapping;
using Domain.Repositories;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Web.Mapping;
using Web.Services;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;

namespace Web
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddControllersWithViews();
            services.AddDistributedMemoryCache();
            services.AddMvc();
            services.AddScoped<ICityPrayerTimesRepository, CityPrayerTimesRepository>();
            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            }
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured. Set ConnectionStrings:DefaultConnection or the CONNECTION_STRING environment variable.");
            }
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseMySql(connectionString,
                    ServerVersion.AutoDetect(connectionString)));

            services.AddHttpClient<IPrayerTimeService, PrayerTimeService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    SslProtocols = SslProtocols.Tls12
                })
                .ConfigureHttpClient(client =>
                {
                    client.DefaultRequestVersion = HttpVersion.Version11;
                    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                });

            services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

            services.AddAutoMapper(cfg => { },
                typeof(MappingProfile).Assembly,
                typeof(DTOToviewModelMappingProfile).Assembly);
            services.AddHostedService<MonthlyPrayerTimesRefreshService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseSession();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
