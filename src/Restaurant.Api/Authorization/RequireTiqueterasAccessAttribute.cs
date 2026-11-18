using Microsoft.AspNetCore.Authorization;
using Restaurant.Application.Authorization;

namespace Restaurant.Api.Authorization;

/// <summary>Catalog products (configure) or payments checkout (sell/redeem context).</summary>
public sealed class RequireTiqueterasAccessAttribute : AuthorizeAttribute
{
    public RequireTiqueterasAccessAttribute()
    {
        Policy = FeatureAuthorizationPolicies.TiqueterasAccess;
    }
}
