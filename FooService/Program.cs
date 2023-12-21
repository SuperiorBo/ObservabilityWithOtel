using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Custom metrics for the application
var fooMeter = new Meter("FooMeter", "1.0.0");
var fooCounter = fooMeter.CreateCounter<int>("FooCounter", description: "Counts the number of foo");

// Custom ActivitySource for the application
var fooActivitySource = new ActivitySource("FooActivitySource");

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
        metrics.AddHttpClientInstrumentation();
        metrics.AddMeter(fooMeter.Name);
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
    tracing.AddSource(fooActivitySource.Name);
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

app.MapGet("/api/foo", Get);

async Task<String> Get(IHttpClientFactory clientFactory, ILogger<Program> logger)
{
    logger.LogInformation("/api/foo called");

    Baggage.SetBaggage("FooBaggage1", "FooValue1");
    Baggage.SetBaggage("FooBaggage2", "FooValue2");


    // Log a message
    logger.LogInformation("Sending to Bar");

    var client = clientFactory.CreateClient();
    var result = await client.GetStringAsync("http://localhost:5002/api/bar");

    // Create a new Activity scoped to the method
    using var activity = fooActivitySource.StartActivity("FooActivity");

    // Add a tag to the Activity
    activity?.SetTag("FooTag", "FooValue");
    activity?.AddEvent(new ActivityEvent("FooEvent"));
    await Task.Delay(100);

    // Increment the custom counter
    fooCounter.Add(1);

    return "Hello World!";
}

app.Run();
