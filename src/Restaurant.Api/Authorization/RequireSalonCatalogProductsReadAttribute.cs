using Microsoft.AspNetCore.Authorization;
using Restaurant.Application.Authorization;

namespace Restaurant.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireSalonCatalogProductsReadAttribute : AuthorizeAttribute
{
    public RequireSalonCatalogProductsReadAttribute()
    {
        Policy = FeatureAuthorizationPolicies.SalonCatalogProductsRead;
    }
}
