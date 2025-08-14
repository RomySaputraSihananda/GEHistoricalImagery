using System.Reflection;
using GEHistoricalImagery.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GEHistoricalImagery;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
		var config = builder.Configuration;

        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ApiResponseWrapperFilter>();
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = config["ApiSettings:Title"] ?? "Hello World API",
                Version = config["ApiSettings:Version"] ?? "1.0.0",
                Description = config["ApiSettings:Description"] ?? "Simple API with authentication"
            });

            c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "Enter your API Key in the field below:",
                Name = "X-Api-Key",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Scheme = "ApiKeyScheme"
            });

            c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "ApiKey"
                        },
                        In = Microsoft.OpenApi.Models.ParameterLocation.Header
                    },
                    new string[] { }
                }
            });
            
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });

        var app = builder.Build();

		app.UseSwagger();
		app.UseSwaggerUI();

        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value?.ToLower();
            
            if (path == "/" || path == "/swagger" || path?.StartsWith("/swagger") == true)
            {
                await next();
                return;
            }

            var validApiKey = app.Configuration["ApiSettings:ApiKey"] ?? "default-api-key";
            
            var providedApiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(providedApiKey) || providedApiKey != validApiKey)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid or missing API key");
                return;
            }

            await next();
        });

        app.MapControllers();

        app.Run(config["ApiSettings:Url"] ?? "http://localhost:4444");
    }
}

