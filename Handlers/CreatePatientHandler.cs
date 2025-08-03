using MediatR;
using PatientApi.Models;
using PatientApi.Services.Interfaces;

namespace PatientApi.src.Handlers
{
    public class CreatePatientHandler : IRequestHandler<CreatePatientCommand, Guid>
    {
        private readonly IPatientPersistenceService _svc;
        public CreatePatientHandler(IPatientPersistenceService svc) { _svc = svc; }
        public Task<Guid> Handle(CreatePatientCommand request, CancellationToken ct) =>
            _svc.CreatePatientAsync(request.Patient);
    }
}