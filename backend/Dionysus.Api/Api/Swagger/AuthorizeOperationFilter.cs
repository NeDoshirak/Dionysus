using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var hasAllowAnonymous = method.IsDefined(typeof(AllowAnonymousAttribute), true)
            || method.DeclaringType?.IsDefined(typeof(AllowAnonymousAttribute), true) == true;
        var hasAuthorize = method.IsDefined(typeof(AuthorizeAttribute), true)
            || method.DeclaringType?.IsDefined(typeof(AuthorizeAttribute), true) == true;

        if (hasAllowAnonymous || !hasAuthorize) return;

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new()
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            }
        };
    }
}
