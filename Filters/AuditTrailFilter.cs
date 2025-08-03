using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PatientApi.Filters
{
    public class AuditTrailFilter : IAsyncActionFilter
    {
        private readonly ILogger<AuditTrailFilter> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AuditTrailFilter(ILogger<AuditTrailFilter> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();

            var user = context.HttpContext.User.Identity?.Name ?? "unknown";
            var role = context.HttpContext.User.FindFirstValue(ClaimTypes.Role) ?? "unknown";
            var actionName = context.ActionDescriptor.DisplayName ?? "unknown";
            var statusCode = executedContext.HttpContext.Response.StatusCode;

            var audit = new
            {
                Action = actionName,
                User = user,
                Role = role,
                Timestamp = DateTime.UtcNow,
                StatusCode = statusCode
            };

            _logger.LogInformation("AUDIT: {@Audit}", audit);

            try
            {
                var endpoint = _configuration["Audit:Endpoint"] ?? "https://audit-service.local/api/audit";
                var client = _httpClientFactory.CreateClient();
                var content = new StringContent(JsonSerializer.Serialize(audit), Encoding.UTF8, "application/json");
                await client.PostAsync(endpoint, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send audit log");
            }
        }
    }
}