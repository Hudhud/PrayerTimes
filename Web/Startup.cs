using System;
using System.IO;
using System.Threading.Tasks;
using Application.Calendars;
using Application.Interfaces;
using Calculation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
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
        services.AddSingleton(provider =>
        {
            var data = Path.Combine(provider.GetRequiredService<IWebHostEnvironment>().WebRootPath, "data");
            return PrayerPlaceDirectory.Load(Path.Combine(data, "prayer-places.json"), Path.Combine(data, "prayer-cities-world.json"));
        });
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
            app.UseHsts();
        }

        app.UseSerilogRequestLogging();
        app.UseHttpsRedirection();
        app.UsePathBase("/en");
        var localization = new RequestLocalizationOptions()
            .SetDefaultCulture("da-DK").AddSupportedCultures("da-DK", "en-GB")
            .AddSupportedUICultures("da-DK", "en-GB");
        localization.RequestCultureProviders.Clear();
        localization.RequestCultureProviders.Add(new CustomRequestCultureProvider(context =>
        {
            var culture = context.Request.PathBase == "/en" ? "en-GB" : "da-DK";
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture));
        }));
        app.UseRequestLocalization(localization);
        if (!environment.IsDevelopment())
        {
            app.UseExceptionHandler("/fejl");
        }
        app.Use(async (context, next) =>
        {
            if (context.Request.PathBase != "/en" && context.Request.Path == "/"
                && HttpMethods.IsGet(context.Request.Method) && context.Request.Cookies[SiteLanguage.Cookie] == "en")
            {
                context.Response.Redirect(context.Request.PathBase + "/en/" + context.Request.QueryString);
                return;
            }
            await next(context);
        });
        // The place catalog is public and contains no per-user data.
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/data"),
            branch => branch.UseResponseCompression());
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                if (context.Context.Request.Path == "/data/prayer-cities-world.json")
                {
                    context.Context.Response.Headers.CacheControl = "public, max-age=86400";
                }
            }
        });
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}
