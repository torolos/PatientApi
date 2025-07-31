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
    [ServiceFilter(typeof(AuditTrailFilter))]
    public class PatientsController : ControllerBase
    {
        private readonly IPatientPersistenceService _persistenceService;
        private readonly ILogger<PatientsController> _logger;
        private readonly IMediator _mediator;

        public PatientsController(IPatientPersistenceService persistenceService, ILogger<PatientsController> logger, IMediator mediator)
        {
            _persistenceService = persistenceService;
            _logger = logger;
            _mediator = mediator;
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "viewer")]
        public async Task<IActionResult> GetPatients([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var patients = await _persistenceService.GetPatientsAsync(page, pageSize);
            _logger.LogInformation("Retrieved patients page {Page}", page);
            return Ok(patients);
        }

        [HttpGet("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "viewer")]
        public async Task<IActionResult> GetPatient(Guid id)
        {
            var patient = await _persistenceService.GetPatientByIdAsync(id);
            _logger.LogInformation("Retrieved patient with ID {Id}", id);
            return patient == null ? NotFound() : Ok(patient);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "manager")]
        public async Task<IActionResult> CreatePatient([FromBody] Patient patient)
        {
            await _persistenceService.CreatePatientAsync(patient);
            _logger.LogInformation("Created patient with ID {Id}", patient.Id);
            return CreatedAtAction(nameof(GetPatient), new { id = patient.Id }, patient);
        }

        [HttpPut("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "manager")]
        public async Task<IActionResult> UpdatePatient(Guid id, [FromBody] Patient patient)
        {
            if (id != patient.Id) return BadRequest();

            await _persistenceService.UpdatePatientAsync(patient);
            _logger.LogInformation("Updated patient with ID {Id}", id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "admin")]
        public async Task<IActionResult> DeletePatient(Guid id)
        {
            await _persistenceService.DeletePatientAsync(id);
            _logger.LogInformation("Deleted patient with ID {Id}", id);
            return NoContent();
        }
    }

}