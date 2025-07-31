// ======================= Program.cs =======================
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatientApi;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// --- Packages you may need ---
// dotnet add package Microsoft.EntityFrameworkCore.Sqlite
// dotnet add package Microsoft.EntityFrameworkCore.SqlServer
// dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
// dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
// dotnet add package MediatR.Extensions.Microsoft.DependencyInjection

// --- HttpClient for introspection & audit ---
builder.Services.AddHttpClient();

// --- Distributed cache (Redis) ---
var redisConfig = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConfig;
    options.InstanceName = "PatientApi:";
});

// --- Choose EF Core provider at runtime ---
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite"; // Sqlite | Postgres | SqlServer
var connStr = builder.Configuration.GetConnectionString(provider) ?? provider switch
{
    "Postgres" => "Host=localhost;Database=patients;Username=postgres;Password=postgres",
    "SqlServer" => "Server=localhost;Database=PatientsDb;Trusted_Connection=True;TrustServerCertificate=True",
    _ => "Data Source=patients.db"
};

if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connStr));
    builder.Services.AddScoped<IPatientPersistenceService, PostgresPatientPersistenceService>();
}
else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(connStr));
    builder.Services.AddScoped<IPatientPersistenceService, SqlServerPatientPersistenceService>();
}
else
{
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connStr));
    builder.Services.AddScoped<IPatientPersistenceService, SqlitePatientPersistenceService>();
}

// --- Filters ---
builder.Services.AddScoped<TokenIntrospectionFilter>();
builder.Services.AddScoped<AuditTrailFilter>();

builder.Services.AddControllers();
builder.Services.AddAuthorization();

// --- MediatR ---
builder.Services.AddMediatR(typeof(Program));

var app = builder.Build();

app.MapControllers();

app.Run();

// ======================= End Program.cs =======================


// ======================= Domain & Infrastructure =======================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace PatientApi
{
    #region Models
    public class Patient
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string PatientNumber { get; set; } = default!; // alphanumeric, independent of Id
        [Required]
        public string FirstName { get; set; } = default!;
        [Required]
        public string LastName { get; set; } = default!;
        [EmailAddress]
        public string? Email { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? PrimaryContactNumber { get; set; }
        public ICollection<AdditionalInformation> AdditionalInformation { get; set; } = new List<AdditionalInformation>();
    }

    public class AdditionalInformation
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string Name { get; set; } = default!;
        public string? Value { get; set; }
        public Guid PatientId { get; set; }
        public Patient? Patient { get; set; }
    }
    #endregion

    #region DbContext
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<AdditionalInformation> AdditionalInformations => Set<AdditionalInformation>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.PatientNumber)
                .IsUnique();

            modelBuilder.Entity<AdditionalInformation>()
                .HasOne(d => d.Patient)
                .WithMany(p => p.AdditionalInformation)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
    #endregion

    #region Persistence Abstraction
    public interface IPatientPersistenceService
    {
        Task<IEnumerable<Patient>> GetPatientsAsync(int page = 1, int pageSize = 10);
        Task<Patient?> GetPatientByIdAsync(Guid id);
        Task<Guid> CreatePatientAsync(Patient patient);
        Task UpdatePatientAsync(Patient patient);
        Task DeletePatientAsync(Guid id);
    }

    // Base EF implementation used by provider-specific services
    public abstract class EfPatientPersistenceBase : IPatientPersistenceService
    {
        protected readonly AppDbContext _context;
        protected readonly ILogger _logger;

        protected EfPatientPersistenceBase(AppDbContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public virtual async Task<IEnumerable<Patient>> GetPatientsAsync(int page = 1, int pageSize = 10)
        {
            return await _context.Patients
                .Include(p => p.AdditionalInformation)
                .AsNoTracking()
                .Skip(Math.Max(0, (page - 1) * pageSize))
                .Take(Math.Max(1, pageSize))
                .ToListAsync();
        }

        public virtual async Task<Patient?> GetPatientByIdAsync(Guid id) =>
            await _context.Patients
                .Include(p => p.AdditionalInformation)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

        public virtual async Task<Guid> CreatePatientAsync(Patient patient)
        {
            await _context.Patients.AddAsync(patient);
            await _context.SaveChangesAsync();
            return patient.Id;
        }

        public virtual async Task UpdatePatientAsync(Patient patient)
        {
            _context.Patients.Update(patient);
            await _context.SaveChangesAsync();
        }

        public virtual async Task DeletePatientAsync(Guid id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient != null)
            {
                _context.Patients.Remove(patient);
                await _context.SaveChangesAsync();
            }
        }
    }

    public class SqlitePatientPersistenceService : EfPatientPersistenceBase
    {
        public SqlitePatientPersistenceService(AppDbContext context, ILogger<SqlitePatientPersistenceService> logger)
            : base(context, logger) { }
    }

    public class PostgresPatientPersistenceService : EfPatientPersistenceBase
    {
        public PostgresPatientPersistenceService(AppDbContext context, ILogger<PostgresPatientPersistenceService> logger)
            : base(context, logger) { }
    }

    public class SqlServerPatientPersistenceService : EfPatientPersistenceBase
    {
        public SqlServerPatientPersistenceService(AppDbContext context, ILogger<SqlServerPatientPersistenceService> logger)
            : base(context, logger) { }
    }
    #endregion

    #region CQRS Requests
    public record GetPatientsQuery(int Page, int PageSize) : IRequest<IEnumerable<Patient>>;
    public record GetPatientByIdQuery(Guid Id) : IRequest<Patient?>;
    public record CreatePatientCommand(Patient Patient) : IRequest<Guid>;
    public record UpdatePatientCommand(Patient Patient) : IRequest<Unit>;
    public record DeletePatientCommand(Guid Id) : IRequest<Unit>;
    #endregion

    #region MediatR Handlers
    public class GetPatientsHandler : IRequestHandler<GetPatientsQuery, IEnumerable<Patient>>
    {
        private readonly IPatientPersistenceService _svc;
        public GetPatientsHandler(IPatientPersistenceService svc) { _svc = svc; }
        public Task<IEnumerable<Patient>> Handle(GetPatientsQuery request, CancellationToken ct) =>
            _svc.GetPatientsAsync(request.Page, request.PageSize);
    }

    public class GetPatientByIdHandler : IRequestHandler<GetPatientByIdQuery, Patient?>
    {
        private readonly IPatientPersistenceService _svc;
        public GetPatientByIdHandler(IPatientPersistenceService svc) { _svc = svc; }
        public Task<Patient?> Handle(GetPatientByIdQuery request, CancellationToken ct) =>
            _svc.GetPatientByIdAsync(request.Id);
    }

    public class CreatePatientHandler : IRequestHandler<CreatePatientCommand, Guid>
    {
        private readonly IPatientPersistenceService _svc;
        public CreatePatientHandler(IPatientPersistenceService svc) { _svc = svc; }
        public Task<Guid> Handle(CreatePatientCommand request, CancellationToken ct) =>
            _svc.CreatePatientAsync(request.Patient);
    }

    public class UpdatePatientHandler : IRequestHandler<UpdatePatientCommand, Unit>
    {
        private readonly IPatientPersistenceService _svc;
        public UpdatePatientHandler(IPatientPersistenceService svc) { _svc = svc; }
        public async Task<Unit> Handle(UpdatePatientCommand request, CancellationToken ct)
        {
            await _svc.UpdatePatientAsync(request.Patient);
            return Unit.Value;
        }
    }

    public class DeletePatientHandler : IRequestHandler<DeletePatientCommand, Unit>
    {
        private readonly IPatientPersistenceService _svc;
        public DeletePatientHandler(IPatientPersistenceService svc) { _svc = svc; }
        public async Task<Unit> Handle(DeletePatientCommand request, CancellationToken ct)
        {
            await _svc.DeletePatientAsync(request.Id);
            return Unit.Value;
        }
    }
    #endregion

    #region Authorization: Token Introspection Filter with Distributed Cache (Redis)
    internal sealed class SerializableClaim
    {
        public string Type { get; set; } = default!;
        public string Value { get; set; } = default!;
    }

    public class TokenIntrospectionFilter : IAsyncAuthorizationFilter
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TokenIntrospectionFilter> _logger;
        private readonly IDistributedCache _cache;
        private readonly IConfiguration _configuration;

        public TokenIntrospectionFilter(
            IHttpClientFactory httpClientFactory,
            ILogger<TokenIntrospectionFilter> logger,
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
    #endregion

    #region Audit Filter (posts to external audit service)
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
    #endregion

    #region API Controller (MediatR + role-protected)
    [ApiController]
    [Route("api/[controller]")]
    [ServiceFilter(typeof(TokenIntrospectionFilter))]
    [ServiceFilter(typeof(AuditTrailFilter))]
    public class PatientsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(IMediator mediator, ILogger<PatientsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // GET: api/patients?page=1&pageSize=20  (viewer)
        [HttpGet]
        [Authorize(Roles = "viewer")]
        public async Task<IActionResult> GetPatients([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var patients = await _mediator.Send(new GetPatientsQuery(page, pageSize));
            _logger.LogInformation("Retrieved patients page {Page} size {Size}", page, pageSize);
            return Ok(patients);
        }

        // GET: api/patients/{id}  (viewer)
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "viewer")]
        public async Task<IActionResult> GetPatient(Guid id)
        {
            var patient = await _mediator.Send(new GetPatientByIdQuery(id));
            if (patient is null) return NotFound();
            return Ok(patient);
        }

        // POST: api/patients  (manager)
        [HttpPost]
        [Authorize(Roles = "manager")]
        public async Task<IActionResult> CreatePatient([FromBody] Patient patient)
        {
            var id = await _mediator.Send(new CreatePatientCommand(patient));
            _logger.LogInformation("Created patient {Id}", id);
            return CreatedAtAction(nameof(GetPatient), new { id }, patient with { Id = id });
        }

        // PUT: api/patients/{id}  (manager)
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        public async Task<IActionResult> UpdatePatient(Guid id, [FromBody] Patient patient)
        {
            if (id != patient.Id) return BadRequest("Route id and body id mismatch");
            await _mediator.Send(new UpdatePatientCommand(patient));
            _logger.LogInformation("Updated patient {Id}", id);
            return NoContent();
        }

        // DELETE: api/patients/{id}  (admin)
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeletePatient(Guid id)
        {
            await _mediator.Send(new DeletePatientCommand(id));
            _logger.LogInformation("Deleted patient {Id}", id);
            return NoContent();
        }
    }
    #endregion
}

// ======================= appsettings.json (example) =======================
/*
{
  "ConnectionStrings": {
    "Sqlite": "Data Source=patients.db",
    "Postgres": "Host=localhost;Database=patients;Username=postgres;Password=postgres",
    "SqlServer": "Server=localhost;Database=PatientsDb;Trusted_Connection=True;TrustServerCertificate=True",
    "Redis": "localhost:6379"
  },
  "Database": {
    "Provider": "Sqlite" // or Postgres, SqlServer
  },
  "Auth": {
    "IntrospectionEndpoint": "https://your-introspection-endpoint"
  },
  "Audit": {
    "Endpoint": "https://audit-service.local/api/audit"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
*/
// ======================= End appsettings.json =======================
