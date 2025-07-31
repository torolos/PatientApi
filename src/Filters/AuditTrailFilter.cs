using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PatientApi.Filters
{
    public class AuditTrailFilter : IAsyncActionFilter
    {
        private readonly ILogger<AuditTrailFilter> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuditTrailFilter(ILogger<AuditTrailFilter> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();

            var user = context.HttpContext.User.Identity?.Name ?? "unknown";
            var role = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == "role")?.Value ?? "unknown";
            var actionName = context.ActionDescriptor.DisplayName;
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
                var client = _httpClientFactory.CreateClient();
                var content = new StringContent(JsonSerializer.Serialize(audit), Encoding.UTF8, "application/json");
                await client.PostAsync("https://audit-service.local/api/audit", content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send audit log");
            }
        }
    }
}