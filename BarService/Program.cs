using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);


var tracingOtlpEndpoint = builder.Configuration["OTLP_ENDPOINT_URL"];
Console.WriteLine(tracingOtlpEndpoint);
var otel = builder.Services.AddOpenTelemetry();

// Configure OpenTelemetry Resources with the application name
otel.ConfigureResource(resource => resource
    .AddService(serviceName: builder.Environment.ApplicationName));

// Add Metrics for ASP.NET Core and our custom metrics and export to Prometheus
otel.WithMetrics(metrics => {
        // Metrics provider from OpenTelemetry
        metrics.AddAspNetCoreInstrumentation();
        // Metrics provides by ASP.NET Core in .NET 8
        metrics.AddMeter("Microsoft.AspNetCore.Hosting");
        metrics.AddMeter("Microsoft.AspNetCore.Server.Kestrel");
        if(tracingOtlpEndpoint != null)
        {
            metrics.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(tracingOtlpEndpoint);
            });
        }
        else
        {
            metrics.AddConsoleExporter();
        }

    });

// Add Tracing for ASP.NET Core and our custom ActivitySource and export to Jaeger
otel.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation();
    tracing.AddHttpClientInstrumentation();
    if (tracingOtlpEndpoint != null)
    {
        tracing.AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri(tracingOtlpEndpoint);
        });
    }
    else
    {
        tracing.AddConsoleExporter();
    }
});

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage = true;
        if (tracingOtlpEndpoint != null)
        {
            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(tracingOtlpEndpoint);
            });
        }
        else
        {
            options.AddConsoleExporter();
        }
    });
});

var app = builder.Build();

app.MapGet("/api/bar", Get);

async Task<String> Get(ILogger<Program> logger)
{
    logger.LogInformation("/api/bar called");

    var baggage1 = Baggage.GetBaggage("FooBaggage1");
    var baggage2 = Baggage.GetBaggage("FooBaggage2");
    
    logger.LogInformation($"FooBaggage1: {baggage1}, FooBaggage2: {baggage2}");

    return "Hello from Bar!";
}

app.Run();
