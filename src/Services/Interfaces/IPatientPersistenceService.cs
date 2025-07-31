using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PatientApi.Models;

namespace PatientApi.Services.Interfaces
{
    public interface IPatientPersistenceService
    {
        Task<IEnumerable<Patient>> GetPatientsAsync(int page = 1, int pageSize = 10);
        Task<Patient> GetPatientByIdAsync(Guid id);
        Task CreatePatientAsync(Patient patient);
        Task UpdatePatientAsync(Patient patient);
        Task DeletePatientAsync(Guid id);
    }
}