using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using ZohoWhatsAppAI.Application;
using ZohoWhatsAppAI.Application.Common.Exceptions;
using ZohoWhatsAppAI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Zoho WhatsApp AI API",
        Version = "v1",
        Description = """
            Automate lead qualification and WhatsApp engagement using Zoho CRM and OpenAI.

            ## Core Endpoints

            | Endpoint | Description |
            |----------|-------------|
            | `POST /api/ai/analyze-lead/{leadId}` | Fetch lead from Zoho CRM and return AI summary, score, and recommended action |
            | `POST /api/whatsapp/generate-message/{leadId}` | Generate a personalized WhatsApp message |
            | `POST /api/crm/save-summary/{leadId}` | Save AI summary (and optional conversation summary) to Zoho CRM Notes |

            ## Supporting Endpoints

            | Endpoint | Description |
            |----------|-------------|
            | `POST /api/whatsapp/build-template` | Build a WhatsApp Business API template with AI |
            | `POST /api/whatsapp/conversation-summary/{leadId}` | Summarize a WhatsApp conversation transcript |

            ## Configuration

            Set `OpenAI:ApiKey`, `Zoho:ClientId`, `Zoho:ClientSecret`, and `Zoho:RefreshToken`.
            Enable `Zoho:UseMockApi` for local development without live Zoho credentials.
            """
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zoho WhatsApp AI API v1");
    options.RoutePrefix = "swagger";
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception.");

        var (title, status) = exception switch
        {
            NotFoundException => ("Not Found", StatusCodes.Status404NotFound),
            ConfigurationException => ("Configuration Error", StatusCodes.Status500InternalServerError),
            ExternalServiceException external => ("External Service Error", external.StatusCode),
            _ => ("Internal Server Error", StatusCodes.Status500InternalServerError)
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = title,
            Detail = exception?.Message ?? "An unexpected error occurred.",
            Status = status
        });
    });
});

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
