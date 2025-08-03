using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using PatientApi.Models;
using PatientApi.Services.Interfaces;

namespace PatientApi.src.Handlers
{
    public class DeletePatientHandler : IRequestHandler<DeletePatientCommand, Unit>
    {
        private readonly IPatientPersistenceService _svc;
        public DeletePatientHandler(IPatientPersistenceService svc) { _svc = svc; }
        public async Task<Unit> Handle(DeletePatientCommand request, CancellationToken ct)
        {
            await _svc.DeletePatientAsync(request.Id);
            return Unit.Value;
        }
    }
}