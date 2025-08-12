using PatientApi.DataContext;

namespace PatientApi.Services
{
    public class SqlitePatientPersistenceService : PatientPersistenceService
    {
        public SqlitePatientPersistenceService(PatientDbContext context, ILogger<SqlitePatientPersistenceService> logger) : base(context, logger)
        {
        }
    }
}