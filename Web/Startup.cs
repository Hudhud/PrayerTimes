using Application.Calendars;
using Application.Interfaces;
using Calculation;
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
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Web.Services;

namespace Web;

public sealed class Startup(IConfiguration configuration)
{
    private readonly bool useLocalCalculation = configuration.GetValue("PrayerTimes:UseLocalCalculation", false);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<DanishTime>();
        services.AddSingleton(provider => new PrayerPlaceDirectory(useLocalCalculation
            ? Path.Combine(provider.GetRequiredService<IWebHostEnvironment>().WebRootPath, "data", "prayer-places.json")
            : null));

        if (useLocalCalculation)
        {
            services.AddSingleton<DanishPrayerTimeCalculator>();
            services.AddSingleton<IPrayerTimeService, LocalPrayerTimeService>();
            return;
        }

        services.AddScoped<ICityPrayerTimesRepository, CityPrayerTimesRepository>();

        var connectionString = configuration["CONNECTION_STRING"]
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection or CONNECTION_STRING in the hosting environment.");
        }

        services.AddDbContextPool<ApplicationDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.AddHttpClient<PrayerTimeService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestVersion = HttpVersion.Version11;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

        services.AddTransient<IPrayerTimeService>(provider => provider.GetRequiredService<PrayerTimeService>());
        services.AddHostedService<MonthlyPrayerTimesRefreshService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        if (!useLocalCalculation && configuration.GetValue("Database:ApplyMigrations", true))
        {
            ApplyDatabaseMigrations(app.ApplicationServices);
        }

        if (environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/fejl");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }

    private static void ApplyDatabaseMigrations(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.OpenConnection();
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandTimeout = 35;
        command.CommandText = "SELECT GET_LOCK(CONCAT(LEFT(DATABASE(), 40), ':salahtimes-migrations'), 30)";
        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
        {
            throw new InvalidOperationException("Could not lock the prayer times database for migrations.");
        }

        try
        {
            context.Database.Migrate();
        }
        finally
        {
            command.CommandText = "SELECT RELEASE_LOCK(CONCAT(LEFT(DATABASE(), 40), ':salahtimes-migrations'))";
            command.ExecuteScalar();
            context.Database.CloseConnection();
        }
    }

}
