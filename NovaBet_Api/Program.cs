using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

using Scalar.AspNetCore;
using Serilog;

using NoviBet_Api.Application.Services.Interfaces;
using NoviBet_Api.Core.CoreServices;
using NoviBet_Api.Application.Services.Services;
using NoviBet_Api.Core.ICoreServices;
using Scalar.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using NoviBet_Api.Core.Models;
using Quartz;
using Shared.Dtos.Settings;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure rate limiting policies
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ClientIpPolicy", context =>
    {
        // Use IP address as the key
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetTokenBucketLimiter(clientIp, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,               // max 10 tokens in bucket
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,                // no queue, reject immediately if no tokens
            ReplenishmentPeriod = TimeSpan.FromSeconds(60),
            TokensPerPeriod = 10,          // replenish 10 tokens every 60 seconds
            AutoReplenishment = true
        });
    });

    options.RejectionStatusCode = 429;
});

builder.Services.AddOptions<CurrencySettings>()
    .Bind(builder.Configuration.GetSection("CurrencySettings"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();

var x = AppDomain.CurrentDomain.GetAssemblies();
//Add the Auto Mapper 

//builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddDbContext<NoviBetContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("NoviBetSQL")));

builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

    var jobKey = new JobKey("CurrencyRatesUpdater");

    q.AddJob<CurrencyRatesUpdaterJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("CurrencyRatesUpdater-trigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInMinutes(1)
            .RepeatForever())
    );
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);



builder.Services.AddScoped<INovibetService , NoviBetService>();
builder.Services.AddHttpClient<INoviBet_Core, Novibet_Core>(); // Preferred

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    // If the client hasn't specified the API version in the request, use the default API version number as the default value
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    //options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Novibet API");
        options.WithTheme(ScalarTheme.BluePlanet);
        options.WithSidebar(true);

    });

    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();
