using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatientApi.Filters;
using PatientApi.Models;
using PatientApi.Services.Interfaces;

namespace PatientApi.src.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ServiceFilter(typeof(TokenIntrospectionFilterDistributedCache))]
    [ServiceFilter(typeof(AuditTrailFilter))]
    public class PatientsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(IMediator mediator, ILogger<PatientsController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        // GET: api/patients?page=1&pageSize=20  (viewer)
        [HttpGet]
        [Authorize(Roles = "viewer")]
        public async Task<IActionResult> GetPatients([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var patients = await _mediator.Send(new GetPatientsQuery(page, pageSize));
            _logger.LogInformation("Retrieved patients page {Page} size {Size}", page, pageSize);
            return Ok(patients);
        }

        // GET: api/patients/{id}  (viewer)
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "viewer")]
        public async Task<IActionResult> GetPatient(Guid id)
        {
            var patient = await _mediator.Send(new GetPatientByIdQuery(id));
            if (patient is null) return NotFound();
            return Ok(patient);
        }

        // POST: api/patients  (manager)
        [HttpPost]
        [Authorize(Roles = "manager")]
        public async Task<IActionResult> CreatePatient([FromBody] Patient patient)
        {
            var id = await _mediator.Send(new CreatePatientCommand(patient));
            _logger.LogInformation("Created patient {Id}", id);
            return CreatedAtAction(nameof(GetPatient), new { id }, patient);
        }

        // PUT: api/patients/{id}  (manager)
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "manager")]
        public async Task<IActionResult> UpdatePatient(Guid id, [FromBody] Patient patient)
        {
            if (id != patient.Id) return BadRequest("Route id and body id mismatch");
            await _mediator.Send(new UpdatePatientCommand(patient));
            _logger.LogInformation("Updated patient {Id}", id);
            return NoContent();
        }

        // DELETE: api/patients/{id}  (admin)
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeletePatient(Guid id)
        {
            await _mediator.Send(new DeletePatientCommand(id));
            _logger.LogInformation("Deleted patient {Id}", id);
            return NoContent();
        }
    }

}