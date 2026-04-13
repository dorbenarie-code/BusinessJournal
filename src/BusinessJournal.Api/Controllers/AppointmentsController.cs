using BusinessJournal.Api.Contracts.Appointments;
using BusinessJournal.Application.Services;
using BusinessJournal.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace BusinessJournal.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _appointmentService;

    public AppointmentsController(AppointmentService appointmentService)
    {
        ArgumentNullException.ThrowIfNull(appointmentService);
        _appointmentService = appointmentService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public ActionResult<AppointmentResponse> Create(ScheduleAppointmentRequest request)
    {
        var appointment = _appointmentService.ScheduleAppointment(
            request.CustomerId,
            request.Title,
            request.StartsAt,
            request.EndsAt,
            request.Notes);

        var response = ToResponse(appointment);

        return CreatedAtAction(
            nameof(GetById),
            new { id = appointment.Id },
            response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AppointmentResponse> GetById(Guid id)
    {
        var appointment = _appointmentService.FindAppointmentById(id);

        if (appointment is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(appointment));
    }

    [HttpGet("by-customer/{customerId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AppointmentResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<AppointmentResponse>> GetByCustomerId(Guid customerId)
    {
        var appointments = _appointmentService.GetAppointmentsForCustomer(customerId)
            .Select(ToResponse)
            .ToList();

        return Ok(appointments);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Cancel(Guid id)
    {
        _appointmentService.CancelAppointment(id);
        return NoContent();
    }

    [HttpPut("{id:guid}/reschedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Reschedule(Guid id, RescheduleAppointmentRequest request)
    {
        _appointmentService.RescheduleAppointment(
            id,
            request.StartsAt,
            request.EndsAt);

        return NoContent();
    }

    private static AppointmentResponse ToResponse(Appointment appointment)
    {
        return new AppointmentResponse
        {
            Id = appointment.Id,
            CustomerId = appointment.CustomerId,
            Title = appointment.Title,
            StartsAt = appointment.StartsAt,
            EndsAt = appointment.EndsAt,
            Notes = appointment.Notes,
            IsCancelled = appointment.IsCancelled
        };
    }
}