using Microsoft.AspNetCore.Authorization;
using Restaurant.Application.Authorization;

namespace Restaurant.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireFeatureAttribute : AuthorizeAttribute
{
    public RequireFeatureAttribute(string featureCode)
    {
        Policy = FeatureAuthorizationPolicies.For(featureCode);
    }
}
