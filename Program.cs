using Microsoft.EntityFrameworkCore;
using MediatR;
using PatientApi.Services.Interfaces;
using PatientApi.DataContext;
using PatientApi.Services;

namespace PatientApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
            var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
            var connStr = builder.Configuration.GetConnectionString(provider) ?? provider switch
            {
                "Postgres" => "Host=localhost;Database=patients;Username=postgres;Password=postgres",
                "SqlServer" => "Server=localhost;Database=PatientsDb;Trusted_Connection=True;TrustServerCertificate=True",
                _ => "Data Source=patients.db"
            };

            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddDbContext<PatientDbContext>(opt => opt.UseNpgsql(connStr));
                builder.Services.AddScoped<IPatientPersistenceService, PostgreSqlPersistenceService>();
            }
            else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddDbContext<PatientDbContext>(opt => opt.UseSqlServer(connStr));
                builder.Services.AddScoped<IPatientPersistenceService, SqlServerPersistenceService>();
            }
            else
            {
                builder.Services.AddDbContext<PatientDbContext>(opt => opt.UseSqlite(connStr));
                builder.Services.AddScoped<IPatientPersistenceService, SqlitePatientPersistenceService>();
            }

            // --- Filters ---
            builder.Services.AddScoped<TokenIntrospectionFilter>();
            builder.Services.AddScoped<AuditTrailFilter>();

            // builder.Services.AddControllers(options =>
            // {
            //     options.Filters.Add<TokenIntrospectionFilter>();
            //     options.Filters.Add<AuditTrailFilter>();
            // }).AddFluentValidation();

            builder.Services.AddAuthorization();
            builder.Services.AddMediatR(typeof(Program));
            // builder.Services.AddValidatorsFromAssemblyContaining<Program>();
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            var app = builder.Build();

            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
