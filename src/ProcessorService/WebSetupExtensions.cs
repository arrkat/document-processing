using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;

namespace ProcessorService;

public static class WebSetupExtensions
{
    /// <summary>
    /// Controllers with enums as strings. Request validation is owned by our validators, so ASP.NET's implicit
    /// "non-nullable means required" check is switched off; malformed JSON still gets an automatic 400.
    /// </summary>
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services;
    }

    /// <summary>Swagger UI at /swagger, documented from the XML comments of this service and of Contracts.</summary>
    public static IServiceCollection AddSwagger(this IServiceCollection services, string title, string description)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1", Description = description });
            foreach (var assembly in new[] { Assembly.GetExecutingAssembly(), typeof(Contracts.DocumentLimits).Assembly })
            {
                var xml = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xml))
                    o.IncludeXmlComments(xml);
            }
        });
        return services;
    }
}
