using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PatientApi.Models;

namespace PatientApi.DataContext
{
    public class PatientDbContext : DbContext
    {
        public PatientDbContext(DbContextOptions<PatientDbContext> options) : base(options) { }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<AdditionalInformation> AdditionalInformations { get; set; }
    }
}