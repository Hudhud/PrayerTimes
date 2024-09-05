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

namespace Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

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
            services.AddScoped<IPrayerTimeService, PrayerTimeService>();
            services.AddScoped<ICityPrayerTimesRepository, CityPrayerTimesRepository>();
            services.AddLogging(config =>
            {
                config.AddConsole();
                config.AddDebug();
            });
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseMySql(Configuration.GetConnectionString("DefaultConnection") ?? Environment.GetEnvironmentVariable("CONNECTION_STRING"),
                    ServerVersion.AutoDetect(Configuration.GetConnectionString("DefaultConnection") ?? Environment.GetEnvironmentVariable("CONNECTION_STRING"))));

            services.AddHttpClient<IPrayerTimeService, PrayerTimeService>();
            services.AddAutoMapper(typeof(MappingProfile));
            services.AddAutoMapper(typeof(DTOToviewModelMappingProfile));
            services.AddHostedService<PrayerTimeUpdateService>();
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
