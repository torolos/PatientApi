using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PatientApi.DataContext;
using PatientApi.Models;
using PatientApi.Services.Interfaces;

namespace PatientApi.Services
{
    public abstract class PatientPersistenceService : IPatientPersistenceService
    {
        protected readonly PatientDbContext _context;
        protected readonly ILogger _logger;

        protected PatientPersistenceService(PatientDbContext context, ILogger<PatientPersistenceService> logger)
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
}