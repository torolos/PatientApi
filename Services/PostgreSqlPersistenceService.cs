using PatientApi.DataContext;

namespace PatientApi.Services
{
    public class PostgreSqlPersistenceService : PatientPersistenceService
    {
        public PostgreSqlPersistenceService(PatientDbContext context, ILogger<PostgreSqlPersistenceService> logger) : base(context, logger)
        {
        }
    }
}