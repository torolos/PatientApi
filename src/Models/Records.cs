using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;

namespace PatientApi.Models
{
    public record GetPatientsQuery(int Page, int PageSize) : IRequest<IEnumerable<Patient>>;
    public record GetPatientByIdQuery(Guid Id) : IRequest<Patient>;
    public record CreatePatientCommand(Patient Patient) : IRequest;
    public record UpdatePatientCommand(Patient Patient) : IRequest;
    public record DeletePatientCommand(Guid Id) : IRequest;
}