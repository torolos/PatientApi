using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace PatientApi.src.Filters
{
    public class TokenIntrospectionFilter : IAsyncAuthorizationFilter
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TokenIntrospectionFilter> _logger;
        private readonly IMemoryCache _cache;

        public TokenIntrospectionFilter(IHttpClientFactory httpClientFactory, ILogger<TokenIntrospectionFilter> logger, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cache = cache;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            if (_cache.TryGetValue(token, out ClaimsPrincipal cachedPrincipal))
            {
                context.HttpContext.User = cachedPrincipal;
                return;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://your-introspection-endpoint");
                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("token", token)
                });
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                var content = await response.Content.ReadAsStringAsync();
                var tokenInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

                if (tokenInfo == null || !tokenInfo.ContainsKey("active") || !(bool)tokenInfo["active"])
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                var claims = new List<Claim>();
                if (tokenInfo.TryGetValue("username", out var username))
                    claims.Add(new Claim(ClaimTypes.Name, username.ToString()));
                if (tokenInfo.TryGetValue("role", out var role))
                    claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

                var identity = new ClaimsIdentity(claims, "Bearer");
                var principal = new ClaimsPrincipal(identity);
                context.HttpContext.User = principal;

                if (tokenInfo.TryGetValue("exp", out var exp) && long.TryParse(exp.ToString(), out var expUnix))
                {
                    var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix) - DateTimeOffset.UtcNow;
                    _cache.Set(token, principal, expiry);
                }
                else
                {
                    _cache.Set(token, principal, TimeSpan.FromMinutes(5));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token introspection failed");
                context.Result = new UnauthorizedResult();
            }
        }
    }
}