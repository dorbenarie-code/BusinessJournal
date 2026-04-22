using System.Net;
using System.Net.Http.Json;

namespace BusinessJournal.Web.Features.Appointments;

public sealed class AppointmentsApiClient
{
    private readonly HttpClient _httpClient;

    public AppointmentsApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<AppointmentResponseModel>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            return [];
        }

        var response = await _httpClient.GetAsync(
            $"/api/appointments/by-customer/{customerId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("The current user is not authenticated.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Unexpected server error. Status code: {(int)response.StatusCode}");
        }

        var appointments = await response.Content.ReadFromJsonAsync<List<AppointmentResponseModel>>(
            cancellationToken: cancellationToken);

        return appointments ?? [];
    }

    public async Task<CreateAppointmentResult> CreateAsync(
        CreateAppointmentInputModel input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!input.TryGetStartsAt(out var startsAt) ||
            !input.TryGetEndsAt(out var endsAt))
        {
            return CreateAppointmentResult.Failure("Start time and end time are required.");
        }

        var request = new
        {
            CustomerId = input.CustomerId,
            Title = input.Title,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Notes = input.Notes
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/appointments",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return CreateAppointmentResult.Failure("You must sign in to create an appointment.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return CreateAppointmentResult.Failure(
                "The submitted form is invalid. Please check the fields and try again.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return CreateAppointmentResult.Failure("The selected customer does not exist.");
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return CreateAppointmentResult.Failure("The requested time slot is not available.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return CreateAppointmentResult.Failure(
                $"Unexpected server error. Status code: {(int)response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<AppointmentResponseModel>(
            cancellationToken: cancellationToken);

        if (payload is null)
        {
            return CreateAppointmentResult.Failure("The server returned an invalid response.");
        }

        return CreateAppointmentResult.Success(payload);
    }

    public async Task<string?> CancelAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        if (appointmentId == Guid.Empty)
        {
            return "Appointment id is required.";
        }

        var response = await _httpClient.PostAsync(
            $"/api/appointments/{appointmentId}/cancel",
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "You must sign in to cancel an appointment.";
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return "The appointment does not exist.";
        }

        if (!response.IsSuccessStatusCode)
        {
            return $"Unexpected server error. Status code: {(int)response.StatusCode}";
        }

        return null;
    }

    public async Task<string?> RescheduleAsync(
        Guid appointmentId,
        RescheduleAppointmentInputModel input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (appointmentId == Guid.Empty)
        {
            return "Appointment id is required.";
        }

        if (!input.TryGetStartsAt(out var startsAt) ||
            !input.TryGetEndsAt(out var endsAt))
        {
            return "Start time and end time are required.";
        }

        var request = new
        {
            StartsAt = startsAt,
            EndsAt = endsAt
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"/api/appointments/{appointmentId}/reschedule",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "You must sign in to reschedule an appointment.";
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return "The submitted form is invalid. Please check the fields and try again.";
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return "The appointment does not exist.";
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return "The requested time slot is not available.";
        }

        if (!response.IsSuccessStatusCode)
        {
            return $"Unexpected server error. Status code: {(int)response.StatusCode}";
        }

        return null;
    }
}