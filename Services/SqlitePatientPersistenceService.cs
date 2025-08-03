using PatientApi.DataContext;

namespace PatientApi.Services
{
    public class SqlitePatientPersistenceService : PatientPersistenceService
    {
        public SqlitePatientPersistenceService(PatientDbContext context, ILogger logger) : base(context, logger)
        {
        }
    }
}