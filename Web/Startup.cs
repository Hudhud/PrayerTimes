using System;
using System.IO;
using Application.Calendars;
using Application.Interfaces;
using Calculation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Web.Services;

namespace Web;

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();
        services.AddResponseCompression(options => options.EnableForHttps = true);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DanishTime>();
        services.AddSingleton(provider => new PrayerPlaceDirectory(
            Path.Combine(provider.GetRequiredService<IWebHostEnvironment>().WebRootPath, "data", "prayer-places.json")));
        services.AddSingleton<DanishPrayerTimeCalculator>();
        services.AddSingleton<IPrayerTimeService, LocalPrayerTimeService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/fejl");
            app.UseHsts();
        }

        app.UseSerilogRequestLogging();
        app.UseHttpsRedirection();
        // The place catalog is public and contains no per-user data.
        app.UseWhen(context => context.Request.Path == "/data/prayer-places.json",
            branch => branch.UseResponseCompression());
        app.UseStaticFiles();
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
