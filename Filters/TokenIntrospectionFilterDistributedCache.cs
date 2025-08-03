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

namespace PatientApi.Filters
{
    public class TokenIntrospectionFilterDistributedCache : IAsyncAuthorizationFilter
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TokenIntrospectionFilterDistributedCache> _logger;
        private readonly IDistributedCache _cache;
        private readonly IConfiguration _configuration;

        public TokenIntrospectionFilterDistributedCache(
            IHttpClientFactory httpClientFactory,
            ILogger<TokenIntrospectionFilterDistributedCache> logger,
            IDistributedCache cache,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cache = cache;
            _configuration = configuration;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            var cached = await _cache.GetStringAsync(token);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                try
                {
                    var items = JsonSerializer.Deserialize<List<SerializableClaim>>(cached) ?? new();
                    var claims = items.Select(i => new Claim(i.Type, i.Value));
                    context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize token claims from cache; will re-introspect.");
                }
            }

            try
            {
                var endpoint = _configuration["Auth:IntrospectionEndpoint"] ?? "https://your-introspection-endpoint";
                var client = _httpClientFactory.CreateClient();

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("token", token)
                        // add client credentials if required by your server
                    })
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Introspection endpoint returned {Status}", response.StatusCode);
                    context.Result = new UnauthorizedResult();
                    return;
                }

                var payload = await response.Content.ReadAsStringAsync();
                var tokenInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);

                if (tokenInfo is null || !tokenInfo.TryGetValue("active", out var activeObj) || !(activeObj is bool active) || !active)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                var claims = new List<Claim>();
                if (tokenInfo.TryGetValue("username", out var username) && username is not null)
                    claims.Add(new Claim(ClaimTypes.Name, username.ToString()!));

                if (tokenInfo.TryGetValue("role", out var roleVal) && roleVal is not null)
                    claims.Add(new Claim(ClaimTypes.Role, roleVal.ToString()!));

                if (tokenInfo.TryGetValue("roles", out var rolesVal) && rolesVal is JsonElement rolesJson && rolesJson.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in rolesJson.EnumerateArray())
                    {
                        if (r.ValueKind == JsonValueKind.String)
                            claims.Add(new Claim(ClaimTypes.Role, r.GetString()!));
                    }
                }

                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
                context.HttpContext.User = principal;

                TimeSpan ttl = TimeSpan.FromMinutes(5);
                if (tokenInfo.TryGetValue("exp", out var expVal))
                {
                    if (long.TryParse(expVal.ToString(), out var expUnix))
                    {
                        var until = DateTimeOffset.FromUnixTimeSeconds(expUnix) - DateTimeOffset.UtcNow;
                        if (until > TimeSpan.Zero && until < TimeSpan.FromDays(7))
                            ttl = until;
                    }
                }

                var serializable = claims.Select(c => new SerializableClaim { Type = c.Type, Value = c.Value }).ToList();
                var json = JsonSerializer.Serialize(serializable);
                await _cache.SetStringAsync(token, json, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token introspection failed");
                context.Result = new UnauthorizedResult();
            }
        }
    }

    internal sealed class SerializableClaim
    {
        public string Type { get; set; } = default!;
        public string Value { get; set; } = default!;
    }
}