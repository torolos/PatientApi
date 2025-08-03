using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using PatientApi.Models;
using PatientApi.Services.Interfaces;

namespace PatientApi.Handlers
{
    public class UpdatePatientHandler : IRequestHandler<UpdatePatientCommand, Unit>
    {
        private readonly IPatientPersistenceService _svc;
        public UpdatePatientHandler(IPatientPersistenceService svc) { _svc = svc; }
        public async Task<Unit> Handle(UpdatePatientCommand request, CancellationToken ct)
        {
            await _svc.UpdatePatientAsync(request.Patient);
            return Unit.Value;
        }
    }
}