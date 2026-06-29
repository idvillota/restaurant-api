using Microsoft.AspNetCore.Authorization;
using Restaurant.Application.Authorization;

namespace Restaurant.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireOperationalCashierContextReadAttribute : AuthorizeAttribute
{
    public RequireOperationalCashierContextReadAttribute()
    {
        Policy = FeatureAuthorizationPolicies.OperationalCashierContextRead;
    }
}
