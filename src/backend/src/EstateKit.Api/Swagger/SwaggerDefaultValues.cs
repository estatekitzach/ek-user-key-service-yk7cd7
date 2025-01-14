using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen; // v6.0.0
using System;
using System.Linq;
using System.Reflection;

namespace EstateKit.Api.Swagger
{
    /// <summary>
    /// Operation filter implementation that enhances Swagger documentation with default values,
    /// versioning, security requirements, and comprehensive response documentation.
    /// </summary>
    public class SwaggerDefaultValues : IOperationFilter
    {
        /// <summary>
        /// Applies default values and enhancements to the Swagger operation documentation.
        /// </summary>
        /// <param name="operation">The OpenAPI operation to be modified</param>
        /// <param name="context">Context containing API description and metadata</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var apiDescription = context.ApiDescription;

            // Set operation deprecation status
            operation.Deprecated = context.ApiDescription.CustomAttributes()
                .OfType<ObsoleteAttribute>()
                .Any();

            // Set operation summary from XML documentation
            if (string.IsNullOrWhiteSpace(operation.Summary) && 
                apiDescription.TryGetMethodInfo(out MethodInfo methodInfo))
            {
                operation.Summary = methodInfo.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()
                    ?.Description;
            }

            // Add API version parameter for versioned endpoints
            if (context.ApiDescription.RelativePath?.Contains("{version}") == true)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "version",
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema { Type = "string", Default = new OpenApiString("v1") },
                    Description = "API version"
                });
            }

            // Configure standard response types
            ConfigureResponseTypes(operation);

            // Add security requirements
            ConfigureSecurityRequirements(operation);

            // Add rate limiting headers
            ConfigureRateLimitingHeaders(operation);

            // Set operation ID
            operation.OperationId = $"{context.ApiDescription.ActionDescriptor.RouteValues["controller"]}_{context.ApiDescription.ActionDescriptor.RouteValues["action"]}";

            // Configure content negotiation
            ConfigureContentNegotiation(operation, context);
        }

        private void ConfigureResponseTypes(OpenApiOperation operation)
        {
            // Ensure all operations have standard response types
            var responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse 
                { 
                    Description = "Success" 
                },
                ["400"] = new OpenApiResponse 
                { 
                    Description = "Bad Request - The request was malformed or contains invalid parameters" 
                },
                ["401"] = new OpenApiResponse 
                { 
                    Description = "Unauthorized - Authentication is required or has failed" 
                },
                ["403"] = new OpenApiResponse 
                { 
                    Description = "Forbidden - The authenticated user does not have the required permissions" 
                },
                ["500"] = new OpenApiResponse 
                { 
                    Description = "Internal Server Error - An unexpected error occurred" 
                }
            };

            foreach (var response in responses)
            {
                if (!operation.Responses.ContainsKey(response.Key))
                {
                    operation.Responses.Add(response.Key, response.Value);
                }
            }
        }

        private void ConfigureSecurityRequirements(OpenApiOperation operation)
        {
            // Add OAuth2 security requirement
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "oauth2"
                            }
                        },
                        new[] { "estatekit_api" }
                    }
                }
            };
        }

        private void ConfigureRateLimitingHeaders(OpenApiOperation operation)
        {
            // Document rate limiting headers
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-RateLimit-Limit",
                In = ParameterLocation.Header,
                Required = false,
                Schema = new OpenApiSchema { Type = "integer" },
                Description = "The maximum number of requests allowed per time window"
            });

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-RateLimit-Remaining",
                In = ParameterLocation.Header,
                Required = false,
                Schema = new OpenApiSchema { Type = "integer" },
                Description = "The number of requests remaining in the current time window"
            });
        }

        private void ConfigureContentNegotiation(OpenApiOperation operation, OperationFilterContext context)
        {
            // Set default content types if not specified
            if (!operation.Responses.Any(r => r.Value.Content?.Any() == true))
            {
                foreach (var response in operation.Responses.Where(r => r.Key.StartsWith("2")))
                {
                    response.Value.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType()
                    };
                }
            }

            // Add content type parameters
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Accept",
                In = ParameterLocation.Header,
                Required = false,
                Schema = new OpenApiSchema { Type = "string", Default = new OpenApiString("application/json") },
                Description = "The requested content type for the response"
            });

            if (operation.RequestBody != null)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "Content-Type",
                    In = ParameterLocation.Header,
                    Required = true,
                    Schema = new OpenApiSchema { Type = "string", Default = new OpenApiString("application/json") },
                    Description = "The content type of the request body"
                });
            }
        }
    }
}