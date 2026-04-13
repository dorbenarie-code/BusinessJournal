using BusinessJournal.Domain.Entities;

namespace BusinessJournal.Application.Interfaces;

public interface IAppointmentRepository
{
    void Add(Appointment appointment);
    void Update(Appointment appointment);
    Appointment? FindById(Guid appointmentId);
    IReadOnlyCollection<Appointment> GetByCustomerId(Guid customerId);
    IReadOnlyCollection<Appointment> GetOverlapping(DateTime startsAt, DateTime endsAt);
}