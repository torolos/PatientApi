using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using PatientApi.Models;
using PatientApi.Services.Interfaces;

namespace PatientApi.src.Handlers
{
    public class GetPatientsHandler : IRequestHandler<GetPatientsQuery, IEnumerable<Patient>>
    {
        private readonly IPatientPersistenceService _svc;
        public GetPatientsHandler(IPatientPersistenceService svc) { _svc = svc; }
        public Task<IEnumerable<Patient>> Handle(GetPatientsQuery request, CancellationToken ct) =>
            _svc.GetPatientsAsync(request.Page, request.PageSize);
    }
}