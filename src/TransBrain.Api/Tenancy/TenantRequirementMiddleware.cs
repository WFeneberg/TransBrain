using Microsoft.AspNetCore.Mvc;

namespace TransBrain.Api.Tenancy;

/// <summary>
/// Refuses authenticated requests whose token carries no usable <c>tenant_id</c> claim.
/// </summary>
/// <remarks>
/// Placed after authorization so authentication/role failures keep their own status codes, and
/// before the endpoints so no handler ever runs for a tenant-less caller. Without this gate the
/// request would still fail (the query filters resolve the tenant and throw, see
/// <see cref="HttpContextTenantContext"/>), but as a 500 instead of a response that names the
/// actual problem: a user that is not assigned to any tenant. Anonymous endpoints (health,
/// OpenAPI in Development) pass through untouched.
/// </remarks>
internal sealed class TenantRequirementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && !HttpContextTenantContext.TryResolve(context.User, out _))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "No tenant assigned.",
                    Detail = $"The access token carries no valid '{HttpContextTenantContext.ClaimType}' claim. "
                             + "Assign the user to a tenant in the identity provider."
                }
            });
            return;
        }

        await next(context);
    }
}
