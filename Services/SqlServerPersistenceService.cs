using PatientApi.DataContext;

namespace PatientApi.Services
{
    public class SqlServerPersistenceService : PatientPersistenceService
    {
        public SqlServerPersistenceService(PatientDbContext context, ILogger logger) : base(context, logger)
        {
        }
    }
}