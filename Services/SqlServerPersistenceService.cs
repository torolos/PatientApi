using PatientApi.DataContext;

namespace PatientApi.Services
{
    public class SqlServerPersistenceService : PatientPersistenceService
    {
        public SqlServerPersistenceService(PatientDbContext context, ILogger<SqlitePatientPersistenceService> logger) : base(context, logger)
        {
        }
    }
}