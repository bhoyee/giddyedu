using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.BuildingBlocks.Time;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Messaging;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Modules.Identity;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.Modules.Subscriptions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

public sealed record IssueAdmissionOfferInput(int ValidForDays = 14);
public sealed record ApplicantOfferInfo(string SchoolName, string ApplicantName, string ApplicationNumber, DateTimeOffset ExpiresAtUtc, AdmissionResponse Response, string? PrimaryColor, string? SecondaryColor);
public sealed record BulkAdmissionDecisionInput(IReadOnlyCollection<Guid> ApplicantIds, int ValidForDays = 14);
public sealed record BulkAdmissionDecisionResult(int Requested, int Succeeded, int Failed, IReadOnlyCollection<Guid> FailedApplicantIds);

public interface IAdmissionDecisionService
{
    Task<Guid> IssueOfferAsync(Guid actor, Guid applicantId, IssueAdmissionOfferInput input, CancellationToken ct = default);
    Task RejectAsync(Guid actor, Guid applicantId, CancellationToken ct = default);
    Task<ApplicantOfferInfo> GetOfferAsync(string token, CancellationToken ct = default);
    Task RespondAsync(string token, bool accepted, CancellationToken ct = default);
    Task<BulkAdmissionDecisionResult> IssueOffersAsync(Guid actor, BulkAdmissionDecisionInput input, CancellationToken ct = default);
    Task<BulkAdmissionDecisionResult> RejectAsync(Guid actor, IReadOnlyCollection<Guid> applicantIds, CancellationToken ct = default);
}

public sealed class AdmissionDecisionService(GiddyEduDbContext db, ITenantContext currentTenant, ITenantContextSetter tenant, IFeatureAccessGuard access, INotificationQueue notifications, IConfiguration configuration, IClock clock) : IAdmissionDecisionService
{
    public async Task<Guid> IssueOfferAsync(Guid actor, Guid applicantId, IssueAdmissionOfferInput input, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        if (input.ValidForDays is < 1 or > 90) throw new ArgumentOutOfRangeException(nameof(input), "Offer validity must be between 1 and 90 days.");
        var applicant = await db.Applicants.SingleOrDefaultAsync(x => x.Id == applicantId, ct) ?? throw new KeyNotFoundException("Applicant was not found.");
        if (string.IsNullOrWhiteSpace(applicant.Email)) throw new InvalidOperationException("Applicant email is required before an offer can be issued.");
        if (applicant.Status is not (ApplicationStatus.UnderReview or ApplicationStatus.Waitlisted)) throw new InvalidOperationException("Only under-review or waitlisted applications can receive an offer.");
        foreach (var previous in await db.AdmissionOffers.Where(x => x.ApplicantId == applicantId && x.Response == AdmissionResponse.Pending).ToListAsync(ct)) previous.Supersede(clock.UtcNow);
        applicant.Transition(ApplicationStatus.Offered, clock.UtcNow);
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48)); var id = Guid.NewGuid(); var expiry = clock.UtcNow.AddDays(input.ValidForDays);
        db.AdmissionOffers.Add(new AdmissionOffer(id, RequireTenant(), applicantId, Hash(rawToken), expiry, clock.UtcNow)); await db.SaveChangesAsync(ct);
        var school = await db.SchoolProfiles.AsNoTracking().Select(x => x.DisplayName).SingleOrDefaultAsync(ct) ?? "GiddyEdu school";
        var link = $"{FrontendBaseUrl()}/admissions/respond?token={Uri.EscapeDataString(rawToken)}";
        var applicantName = $"{applicant.FirstName} {applicant.LastName}";
        var content = GiddyEduEmailTemplate.Create("Your admission offer is ready", $"Dear {applicantName}, {school} has issued an admission offer for you.", "View offer and respond", link, supportingText: $"Please respond before {expiry:dd MMMM yyyy}. This secure link is unique to your application.");
        var payload = JsonSerializer.Serialize(new EmailNotificationPayload($"Admission offer from {school}", content.HtmlBody, content.TextBody));
        await notifications.EnqueueAsync("email", applicant.Email, "admissions.offer", payload, ct); return id;
    }

    public async Task RejectAsync(Guid actor, Guid applicantId, CancellationToken ct = default)
    {
        await access.DemandAsync(actor, Permissions.AdmissionsManage, FeatureKeys.Admissions, ct);
        var applicant = await db.Applicants.SingleOrDefaultAsync(x => x.Id == applicantId, ct) ?? throw new KeyNotFoundException("Applicant was not found.");
        if (applicant.Status is not (ApplicationStatus.UnderReview or ApplicationStatus.Waitlisted or ApplicationStatus.Offered)) throw new InvalidOperationException("This application cannot be rejected from its current status.");
        foreach (var offer in await db.AdmissionOffers.Where(x => x.ApplicantId == applicantId && x.Response == AdmissionResponse.Pending).ToListAsync(ct)) offer.Supersede(clock.UtcNow);
        applicant.Transition(ApplicationStatus.Rejected, clock.UtcNow); await db.SaveChangesAsync(ct);
        if (!string.IsNullOrWhiteSpace(applicant.Email)) { var content = GiddyEduEmailTemplate.Create("Admission application update", "Your admission application was not successful on this occasion.", supportingText: "Please contact the school directly if you need further information about this decision."); var payload = JsonSerializer.Serialize(new EmailNotificationPayload("Admission application update", content.HtmlBody, content.TextBody)); await notifications.EnqueueAsync("email", applicant.Email, "admissions.rejected", payload, ct); }
    }

    public async Task<ApplicantOfferInfo> GetOfferAsync(string token, CancellationToken ct = default)
    {
        var offer = await FindPublicOfferAsync(token, ct); SetTenant(offer.TenantId);
        try { var applicant = await db.Applicants.AsNoTracking().SingleAsync(x => x.Id == offer.ApplicantId, ct); var school = await db.SchoolProfiles.AsNoTracking().SingleOrDefaultAsync(ct); return new(school?.DisplayName ?? "GiddyEdu school", $"{applicant.FirstName} {applicant.LastName}", applicant.ApplicationNumber, offer.ExpiresAtUtc, offer.Response, school?.PrimaryColor, school?.SecondaryColor); }
        finally { tenant.Clear(); }
    }

    public async Task RespondAsync(string token, bool accepted, CancellationToken ct = default)
    {
        var offer = await FindPublicOfferAsync(token, ct); SetTenant(offer.TenantId);
        try { offer = await db.AdmissionOffers.SingleAsync(x => x.Id == offer.Id, ct); var applicant = await db.Applicants.SingleAsync(x => x.Id == offer.ApplicantId, ct); offer.Respond(accepted, clock.UtcNow); applicant.Transition(accepted ? ApplicationStatus.Accepted : ApplicationStatus.Rejected, clock.UtcNow); await db.SaveChangesAsync(ct); }
        finally { tenant.Clear(); }
    }

    public async Task<BulkAdmissionDecisionResult> IssueOffersAsync(Guid actor, BulkAdmissionDecisionInput input, CancellationToken ct = default)
    {
        var ids = ValidateBatch(input.ApplicantIds); var failed = new List<Guid>(); var succeeded = 0;
        foreach (var id in ids) { try { await IssueOfferAsync(actor, id, new(input.ValidForDays), ct); succeeded++; } catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException) { failed.Add(id); } }
        return new(ids.Length, succeeded, failed.Count, failed);
    }

    public async Task<BulkAdmissionDecisionResult> RejectAsync(Guid actor, IReadOnlyCollection<Guid> applicantIds, CancellationToken ct = default)
    {
        var ids = ValidateBatch(applicantIds); var failed = new List<Guid>(); var succeeded = 0;
        foreach (var id in ids) { try { await RejectAsync(actor, id, ct); succeeded++; } catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException) { failed.Add(id); } }
        return new(ids.Length, succeeded, failed.Count, failed);
    }

    private async Task<AdmissionOffer> FindPublicOfferAsync(string token, CancellationToken ct) { if (string.IsNullOrWhiteSpace(token) || token.Length > 200) throw new KeyNotFoundException("Offer was not found."); return await db.AdmissionOffers.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == Hash(token), ct) ?? throw new KeyNotFoundException("Offer was not found."); }
    private void SetTenant(Guid tenantId) => tenant.Set(tenantId, null);
    private Guid RequireTenant() => currentTenant.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    private string FrontendBaseUrl() => (configuration["App:PublicBaseUrl"] ?? configuration["Frontend:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static Guid[] ValidateBatch(IReadOnlyCollection<Guid> applicantIds) { var ids = applicantIds.Where(id => id != Guid.Empty).Distinct().ToArray(); if (ids.Length == 0 || ids.Length > 100) throw new ArgumentException("Select between 1 and 100 applicants.", nameof(applicantIds)); return ids; }
}
