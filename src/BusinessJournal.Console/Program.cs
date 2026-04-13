using BusinessJournal.Application.Common.Exceptions;
using BusinessJournal.Application.Services;
using BusinessJournal.Infrastructure.Repositories;

var customerRepository = new InMemoryCustomerRepository();
var appointmentRepository = new InMemoryAppointmentRepository();

var customerService = new CustomerService(customerRepository);
var appointmentService = new AppointmentService(customerRepository, appointmentRepository);

WriteHeader("Business Journal Demo");

var firstCustomer = customerService.RegisterCustomer(
    "Rachel Cohen",
    "0501234567",
    "rachel@gmail.com");

var secondCustomer = customerService.RegisterCustomer(
    "Michal Levi",
    "0521234567");

WriteSuccess($"Customer created: {firstCustomer.FullName} ({firstCustomer.Id})");
WriteSuccess($"Customer created: {secondCustomer.FullName} ({secondCustomer.Id})");

var firstAppointment = appointmentService.ScheduleAppointment(
    firstCustomer.Id,
    "Hair Color",
    new DateTime(2026, 4, 10, 10, 0, 0),
    new DateTime(2026, 4, 10, 11, 0, 0),
    "First visit");

WriteSuccess(
    $"Appointment created for {firstCustomer.FullName}: " +
    $"{firstAppointment.Title} | {firstAppointment.StartsAt:g} - {firstAppointment.EndsAt:g}");

try
{
    appointmentService.ScheduleAppointment(
        secondCustomer.Id,
        "Hair Cut",
        new DateTime(2026, 4, 10, 10, 30, 0),
        new DateTime(2026, 4, 10, 11, 30, 0));
}
catch (ConflictException ex)
{
    WriteError($"Could not schedule overlapping appointment: {ex.Message}");
}

appointmentService.CancelAppointment(firstAppointment.Id);
WriteInfo("First appointment was cancelled.");

var secondAppointment = appointmentService.ScheduleAppointment(
    secondCustomer.Id,
    "Hair Cut",
    new DateTime(2026, 4, 10, 10, 30, 0),
    new DateTime(2026, 4, 10, 11, 30, 0));

WriteSuccess(
    $"Appointment created for {secondCustomer.FullName}: " +
    $"{secondAppointment.Title} | {secondAppointment.StartsAt:g} - {secondAppointment.EndsAt:g}");

WriteHeader("Appointments For Michal Levi");

var michalAppointments = appointmentService.GetAppointmentsForCustomer(secondCustomer.Id);

foreach (var appointment in michalAppointments)
{
    Console.WriteLine(
        $"- {appointment.Title} | {appointment.StartsAt:g} - {appointment.EndsAt:g} | Cancelled: {appointment.IsCancelled}");
}

static void WriteHeader(string text)
{
    Console.WriteLine();
    Console.WriteLine($"=== {text} ===");
}

static void WriteSuccess(string text)
{
    Console.WriteLine($"[OK] {text}");
}

static void WriteInfo(string text)
{
    Console.WriteLine($"[INFO] {text}");
}

static void WriteError(string text)
{
    Console.WriteLine($"[ERROR] {text}");
}