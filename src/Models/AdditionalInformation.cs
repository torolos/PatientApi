using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace PatientApi.Models
{
    public class AdditionalInformation
    {
        [Key]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public Guid PatientId { get; set; }
    }
}