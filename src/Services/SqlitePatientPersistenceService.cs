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
    public class SqlitePatientPersistenceService : IPatientPersistenceService
    {
        private readonly PatientDbContext _context;
        public SqlitePatientPersistenceService(PatientDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Patient>> GetPatientsAsync(int page = 1, int pageSize = 10)
        {
            return await _context.Patients
                .Include(p => p.AdditionalInformation)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Patient> GetPatientByIdAsync(Guid id) => await _context.Patients.Include(p => p.AdditionalInformation).FirstOrDefaultAsync(p => p.Id == id);

        public async Task CreatePatientAsync(Patient patient)
        {
            await _context.Patients.AddAsync(patient);
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePatientAsync(Patient patient)
        {
            _context.Patients.Update(patient);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePatientAsync(Guid id)
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