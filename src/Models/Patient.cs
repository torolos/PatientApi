using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace PatientApi.Models
{
    public class Patient
    {
        [Key]
        public Guid Id { get; set; }
        public string PatientNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string PrimaryContactNumber { get; set; }
        public ICollection<AdditionalInformation> AdditionalInformation { get; set; }
    }
}