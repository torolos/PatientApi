using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using PatientApi.Models;
using PatientApi.Services.Interfaces;

namespace PatientApi.src.Handlers
{
    public class GetPatientByIdHandler : IRequestHandler<GetPatientByIdQuery, Patient?>
    {
        private readonly IPatientPersistenceService _svc;
        public GetPatientByIdHandler(IPatientPersistenceService svc) { _svc = svc; }
        public Task<Patient?> Handle(GetPatientByIdQuery request, CancellationToken ct) =>
            _svc.GetPatientByIdAsync(request.Id);
    }
}