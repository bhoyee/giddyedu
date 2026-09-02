using System.Text.Json;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Messaging;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public enum ApplicantCommunicationType { General, Interview, Offer, Decision }
public sealed record ApplicantCommunicationInput(ApplicantCommunicationType Type, string Subject, string Message);

public interface IAdmissionsCommunicationService { Task<Guid> SendAsync(Guid actor, Guid applicantId, ApplicantCommunicationInput input, CancellationToken ct = default); }

public sealed class AdmissionsCommunicationService(GiddyEduDbContext db, IFeatureAccessGuard access, INotificationQueue notifications) : IAdmissionsCommunicationService
{
    public async Task<Guid> SendAsync(Guid actor, Guid applicantId, ApplicantCommunicationInput input, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        var applicant = await db.Applicants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicantId, ct) ?? throw new KeyNotFoundException("Applicant was not found.");
        if (string.IsNullOrWhiteSpace(applicant.Email)) throw new InvalidOperationException("Applicant email is required before communication can be sent.");
        if (input.Type == ApplicantCommunicationType.Offer && applicant.Status is not (ApplicationStatus.Offered or ApplicationStatus.Accepted)) throw new InvalidOperationException("Offer communication can only be sent for an offered or accepted application.");
        var subject = Required(input.Subject, 200); var message = Required(input.Message, 5000);
        var encoded = System.Net.WebUtility.HtmlEncode(message).Replace("\r\n", "<br>").Replace("\n", "<br>");
        var payload = JsonSerializer.Serialize(new EmailNotificationPayload(subject, $"<p>{encoded}</p>", message));
        return await notifications.EnqueueAsync("email", applicant.Email, $"admissions.{input.Type.ToString().ToLowerInvariant()}", payload, ct);
    }

    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"Value is required and must not exceed {max} characters.") : value.Trim();
}
