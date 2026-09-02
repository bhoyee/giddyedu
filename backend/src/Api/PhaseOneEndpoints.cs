using System.Security.Claims;
using System.Text.Json;
using GiddyEdu.Infrastructure.Academics;
using GiddyEdu.Infrastructure.Platform;
using GiddyEdu.Infrastructure.Schools;
using GiddyEdu.Infrastructure.Hr;
using GiddyEdu.Infrastructure.Identity;
using GiddyEdu.Modules.Hr.Domain;
using GiddyEdu.Infrastructure.StudentLifecycle;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using GiddyEdu.BuildingBlocks.Tenancy;
using GiddyEdu.Infrastructure.Authorization;
using GiddyEdu.Infrastructure.Subscriptions;
using GiddyEdu.Infrastructure.Storage;
using GiddyEdu.Modules.Subscriptions;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.BuildingBlocks.Time;
using Microsoft.EntityFrameworkCore;

public static class PhaseOneEndpoints
{
    public sealed record CompleteDocumentUploadInput(string Checksum);
    public static IEndpointRouteBuilder MapPhaseOneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/plans", async (ISubscriptionManagementService service, CancellationToken ct) => Results.Ok(await service.ListPlansAsync(ct))).AllowAnonymous();
        endpoints.MapPost("/api/v1/public/admissions/{tenantSlug}/applications", SubmitPublicApplicationAsync).AllowAnonymous().RequireRateLimiting("auth");
        endpoints.MapGet("/api/v1/access/me", GetAccessContextAsync);
        endpoints.MapPost("/api/v1/account-invitations", CreateAccountInvitationAsync);
        endpoints.MapPost("/api/v1/account-invitations/accept", AcceptAccountInvitationAsync).AllowAnonymous().RequireRateLimiting("auth");
        var documents = endpoints.MapGroup("/api/v1/documents");
        documents.MapGet("/{entityType}/{entityId:guid}", ListDocumentsAsync);
        documents.MapPost("/{entityType}/{entityId:guid}/uploads", BeginDocumentUploadAsync);
        documents.MapPost("/{fileId:guid}/complete", CompleteDocumentUploadAsync);
        documents.MapGet("/{fileId:guid}/download", DownloadDocumentAsync);
        documents.MapDelete("/{fileId:guid}", DeleteDocumentAsync);
        var subscriptions = endpoints.MapGroup("/api/v1/subscriptions");
        subscriptions.MapGet("/current", GetCurrentSubscriptionAsync);
        subscriptions.MapPost("/{planCode}", SubscribeAsync);

        var schools = endpoints.MapGroup("/api/v1/schools");
        schools.MapGet("/current", GetSchoolProfileAsync);
        schools.MapPut("/current", UpsertSchoolProfileAsync);
        schools.MapGet("/campuses", ListCampusesAsync);
        schools.MapPost("/campuses", CreateCampusAsync);
        schools.MapPut("/campuses/{campusId:guid}", UpdateCampusAsync);
        schools.MapDelete("/campuses/{campusId:guid}", DeactivateCampusAsync);

        var academics = endpoints.MapGroup("/api/v1/academics");
        academics.MapGet("/structure", GetAcademicStructureAsync);
        academics.MapPost("/years", CreateAcademicYearAsync);
        academics.MapPost("/years/{academicYearId:guid}/activate", ActivateAcademicYearAsync);
        academics.MapPost("/terms", CreateTermAsync);
        academics.MapPost("/education-stages", CreateEducationStageAsync);
        academics.MapPost("/class-levels", CreateClassLevelAsync);
        academics.MapPost("/class-sections", CreateClassSectionAsync);
        academics.MapPost("/departments", CreateDepartmentAsync);
        academics.MapPost("/subjects", CreateSubjectAsync);
        academics.MapPut("/class-subjects", AssignSubjectAsync);

        var hr = endpoints.MapGroup("/api/v1/hr");
        hr.MapGet("/positions", ListPositionsAsync);
        hr.MapPost("/positions", CreatePositionAsync);
        hr.MapGet("/staff", ListStaffAsync);
        hr.MapGet("/staff/{staffId:guid}", GetStaffAsync);
        hr.MapPost("/staff", CreateStaffAsync);
        hr.MapPut("/staff/{staffId:guid}", UpdateStaffAsync);
        hr.MapPut("/staff/{staffId:guid}/status", SetStaffStatusAsync);
        hr.MapPut("/staff/{staffId:guid}/user", LinkStaffUserAsync);
        hr.MapGet("/staff/{staffId:guid}/sensitive", GetStaffSensitiveAsync);
        hr.MapPut("/staff/{staffId:guid}/sensitive", UpsertStaffSensitiveAsync);
        hr.MapGet("/teaching-assignments", ListTeachingAssignmentsAsync);
        hr.MapPost("/teaching-assignments", CreateTeachingAssignmentAsync);
        hr.MapDelete("/teaching-assignments/{assignmentId:guid}", DeleteTeachingAssignmentAsync);

        var admissions = endpoints.MapGroup("/api/v1/admissions");
        admissions.MapGet("/applicants", ListApplicantsAsync);
        admissions.MapPost("/applicants", CreateApplicantAsync);
        admissions.MapPut("/applicants/{applicantId:guid}/status", TransitionApplicantAsync);
        admissions.MapGet("/applicants/{applicantId:guid}/sensitive", GetApplicantSensitiveAsync);
        admissions.MapPut("/applicants/{applicantId:guid}/sensitive", UpsertApplicantSensitiveAsync);
        admissions.MapGet("/applicants/{applicantId:guid}/review", GetAdmissionReviewAsync);
        admissions.MapPut("/applicants/{applicantId:guid}/review", UpsertAdmissionReviewAsync);
        admissions.MapGet("/applicants/{applicantId:guid}/interviews", ListAdmissionInterviewsAsync);
        admissions.MapPost("/applicants/{applicantId:guid}/interviews", ScheduleAdmissionInterviewAsync);
        admissions.MapPut("/interviews/{interviewId:guid}/outcome", CompleteAdmissionInterviewAsync);
        admissions.MapPost("/applicants/{applicantId:guid}/convert", ConvertApplicantAsync);
        admissions.MapPost("/applicants/{applicantId:guid}/communications", SendApplicantCommunicationAsync);
        var students = endpoints.MapGroup("/api/v1/students");
        students.MapGet("/", ListStudentsAsync);
        students.MapGet("/{studentId:guid}", GetStudentAsync);
        students.MapPut("/{studentId:guid}", UpdateStudentAsync);
        students.MapPost("/{studentId:guid}/enrollments", EnrollStudentAsync);
        students.MapPost("/{studentId:guid}/withdraw", WithdrawStudentAsync);
        students.MapGet("/{studentId:guid}/sensitive", GetStudentSensitiveAsync);
        students.MapPut("/{studentId:guid}/sensitive", UpsertStudentSensitiveAsync);
        students.MapPost("/{studentId:guid}/guardians", LinkGuardianAsync);
        var guardians = endpoints.MapGroup("/api/v1/guardians");
        guardians.MapGet("/", ListGuardiansAsync);
        guardians.MapGet("/{guardianId:guid}", GetGuardianAsync);
        guardians.MapPost("/", CreateGuardianAsync);
        guardians.MapPut("/{guardianId:guid}", UpdateGuardianAsync);
        return endpoints;
    }

    public sealed record PublicApplicationInput(string FirstName, string LastName, DateOnly DateOfBirth, string? Email, string? Phone, string? PreviousSchool, string? Source);

    private static async Task<IResult> SubmitPublicApplicationAsync(string tenantSlug, PublicApplicationInput input, GiddyEduDbContext db, ITenantContextSetter tenantContext, IEntitlementService entitlements, IClock clock, CancellationToken ct)
    {
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        var tenantId = await db.Tenants.IgnoreQueryFilters().Where(x => x.Slug == normalizedSlug && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (!tenantId.HasValue) return Results.NotFound();
        tenantContext.Set(tenantId.Value, null);
        try
        {
            if (!(await entitlements.GetAsync(FeatureKeys.Admissions, null, ct)).Enabled) return Results.NotFound();
            var applicantId = Guid.NewGuid(); var applicationNumber = $"APP-{clock.UtcNow:yyyy}-{applicantId.ToString("N")[..10].ToUpperInvariant()}";
            var applicant = new Applicant(applicantId, tenantId.Value, applicationNumber, input.FirstName, input.LastName, input.DateOfBirth, input.Email, input.Phone, input.PreviousSchool, input.Source, clock.UtcNow);
            applicant.Transition(ApplicationStatus.Submitted, clock.UtcNow); db.Applicants.Add(applicant); await db.SaveChangesAsync(ct);
            return Results.Accepted($"/api/v1/public/admissions/{normalizedSlug}/applications/{applicantId}", new { id = applicantId, applicationNumber });
        }
        finally { tenantContext.Clear(); }
    }

    private static async Task<IResult> GetAccessContextAsync(ClaimsPrincipal principal, ITenantContext tenant, IPermissionService permissions, IEntitlementService entitlements, CancellationToken ct)
    {
        var actor = UserId(principal); var effectivePermissions = await permissions.GetEffectivePermissionsAsync(actor, ct);
        var effectiveEntitlements = new Dictionary<string, EffectiveEntitlement>();
        foreach (var featureKey in FeatureKeys.PhaseOne) effectiveEntitlements[featureKey] = await entitlements.GetAsync(featureKey, tenant.CampusId, ct);
        return Results.Ok(new { userId = actor, tenantId = tenant.TenantId, campusId = tenant.CampusId, permissions = effectivePermissions, entitlements = effectiveEntitlements });
    }
    private static async Task<IResult> CreateAccountInvitationAsync(CreateAccountInvitationInput input, ClaimsPrincipal principal, IAccountInvitationService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var invitation = await service.CreateAsync(actor, input, ct); await audit.WriteAsync(actor, "AccountInvitation.Create", "AccountInvitation", invitation.Id.ToString(), "Succeeded", JsonSerializer.Serialize(new { invitation.TargetType, invitation.TargetId }), ct); return Results.Accepted($"/api/v1/account-invitations/{invitation.Id}", invitation); }
    private static async Task<IResult> AcceptAccountInvitationAsync(AcceptAccountInvitationInput input, IAccountInvitationService service, CancellationToken ct)
    { var tenantId = await service.AcceptAsync(input, ct); return Results.Ok(new { tenantId }); }
    private static async Task<IResult> ListDocumentsAsync(string entityType, Guid entityId, ClaimsPrincipal principal, IPhaseOneDocumentService service, CancellationToken ct)
    { return Results.Ok(await service.ListAsync(UserId(principal), entityType, entityId, ct)); }
    private static async Task<IResult> BeginDocumentUploadAsync(string entityType, Guid entityId, BeginDocumentUploadInput input, ClaimsPrincipal principal, IPhaseOneDocumentService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var upload = await service.BeginUploadAsync(actor, entityType, entityId, input, ct); await audit.WriteAsync(actor, "Document.BeginUpload", "StoredFile", upload.FileId.ToString(), "Succeeded", JsonSerializer.Serialize(new { entityType, entityId, input.Category }), ct); return Results.Created($"/api/v1/documents/{upload.FileId}", upload); }
    private static async Task<IResult> CompleteDocumentUploadAsync(Guid fileId, CompleteDocumentUploadInput input, ClaimsPrincipal principal, IPhaseOneDocumentService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.CompleteUploadAsync(actor, fileId, input.Checksum, ct); await audit.WriteAsync(actor, "Document.CompleteUpload", "StoredFile", fileId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> DownloadDocumentAsync(Guid fileId, ClaimsPrincipal principal, IPhaseOneDocumentService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var url = await service.CreateDownloadUrlAsync(actor, fileId, ct); await audit.WriteAsync(actor, "Document.Download", "StoredFile", fileId.ToString(), "Succeeded", null, ct); return Results.Ok(new { url }); }
    private static async Task<IResult> DeleteDocumentAsync(Guid fileId, ClaimsPrincipal principal, IPhaseOneDocumentService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.DeleteAsync(actor, fileId, ct); await audit.WriteAsync(actor, "Document.Delete", "StoredFile", fileId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> GetCurrentSubscriptionAsync(ClaimsPrincipal principal, ISubscriptionManagementService service, CancellationToken ct)
    { var subscription = await service.GetCurrentAsync(UserId(principal), ct); return subscription is null ? Results.NotFound() : Results.Ok(subscription); }
    private static async Task<IResult> SubscribeAsync(string planCode, ClaimsPrincipal principal, ISubscriptionManagementService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var id = await service.SubscribeAsync(actor, planCode, ct); await audit.WriteAsync(actor, "Subscription.Start", "TenantSubscription", id.ToString(), "Succeeded", JsonSerializer.Serialize(new { planCode }), ct); return Results.Created($"/api/v1/subscriptions/{id}", new { id }); }

    private static async Task<IResult> GetSchoolProfileAsync(ClaimsPrincipal principal, ISchoolAdministrationService service, CancellationToken ct)
    { var profile = await service.GetProfileAsync(UserId(principal), ct); return profile is null ? Results.NotFound() : Results.Ok(profile); }
    private static async Task<IResult> UpsertSchoolProfileAsync(SchoolProfileInput input, ClaimsPrincipal principal, ISchoolAdministrationService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpsertProfileAsync(actor, input, ct); await audit.WriteAsync(actor, "SchoolProfile.Upsert", "SchoolProfile", "current", "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> ListCampusesAsync(ClaimsPrincipal principal, ISchoolAdministrationService service, CancellationToken ct) => Results.Ok(await service.ListCampusesAsync(UserId(principal), ct));
    private static async Task<IResult> CreateCampusAsync(CampusInput input, ClaimsPrincipal principal, ISchoolAdministrationService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var id = await service.CreateCampusAsync(actor, input, ct); await audit.WriteAsync(actor, "Campus.Create", "Campus", id.ToString(), "Succeeded", null, ct); return Results.Created($"/api/v1/schools/campuses/{id}", new { id }); }
    private static async Task<IResult> UpdateCampusAsync(Guid campusId, CampusInput input, ClaimsPrincipal principal, ISchoolAdministrationService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpdateCampusAsync(actor, campusId, input, ct); await audit.WriteAsync(actor, "Campus.Update", "Campus", campusId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> DeactivateCampusAsync(Guid campusId, ClaimsPrincipal principal, ISchoolAdministrationService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.DeactivateCampusAsync(actor, campusId, ct); await audit.WriteAsync(actor, "Campus.Deactivate", "Campus", campusId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }

    private static async Task<IResult> GetAcademicStructureAsync(Guid? academicYearId, ClaimsPrincipal principal, IAcademicStructureService service, CancellationToken ct) => Results.Ok(await service.GetAsync(UserId(principal), academicYearId, ct));
    private static async Task<IResult> CreateAcademicYearAsync(AcademicYearInput input, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct) => await CreatedAsync(await service.CreateAcademicYearAsync(UserId(principal), input, ct), "AcademicYear", "AcademicYear.Create", principal, audit, ct);
    private static async Task<IResult> ActivateAcademicYearAsync(Guid academicYearId, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.ActivateAcademicYearAsync(actor, academicYearId, ct); await audit.WriteAsync(actor, "AcademicYear.Activate", "AcademicYear", academicYearId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> CreateTermAsync(AcademicTermInput input, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct) => await CreatedAsync(await service.CreateTermAsync(UserId(principal), input, ct), "AcademicTerm", "AcademicTerm.Create", principal, audit, ct);
    private static async Task<IResult> CreateEducationStageAsync(EducationStageInput input, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct) => await CreatedAsync(await service.CreateEducationStageAsync(UserId(principal), input, ct), "EducationStage", "EducationStage.Create", principal, audit, ct);
    private static async Task<IResult> CreateClassLevelAsync(ClassLevelInput input, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct) => await CreatedAsync(await service.CreateClassLevelAsync(UserId(principal), input, ct), "ClassLevel", "ClassLevel.Create", principal, audit, ct);
    private static async Task<IResult> CreateClassSectionAsync(ClassSectionInput input, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct) => await CreatedAsync(await service.CreateClassSectionAsync(UserId(principal), input, ct), "ClassSection", "ClassSection.Create", principal, audit, ct);
    private static async Task<IResult> CreateDepartmentAsync(DepartmentInput input, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct) => await CreatedAsync(await service.CreateDepartmentAsync(UserId(principal), input, ct), "Department", "Department.Create", principal, audit, ct);
    private static async Task<IResult> CreateSubjectAsync(SubjectInput input, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct) => await CreatedAsync(await service.CreateSubjectAsync(UserId(principal), input, ct), "Subject", "Subject.Create", principal, audit, ct);
    private static async Task<IResult> AssignSubjectAsync(ClassSubjectInput input, ClaimsPrincipal principal, IAcademicStructureService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.AssignSubjectAsync(actor, input, ct); await audit.WriteAsync(actor, "ClassSubject.Assign", "ClassSection", input.ClassSectionId.ToString(), "Succeeded", JsonSerializer.Serialize(new { input.SubjectId }), ct); return Results.NoContent(); }

    private static async Task<IResult> ListPositionsAsync(ClaimsPrincipal principal, IStaffService service, CancellationToken ct) => Results.Ok(await service.ListPositionsAsync(UserId(principal), ct));
    private static async Task<IResult> CreatePositionAsync(PositionInput input, ClaimsPrincipal principal, IStaffService service, IAuditWriter audit, CancellationToken ct) => await CreatedHrAsync(await service.CreatePositionAsync(UserId(principal), input, ct), "Position", "Position.Create", principal, audit, ct);
    private static async Task<IResult> ListStaffAsync(int page, int pageSize, string? search, StaffStatus? status, ClaimsPrincipal principal, IStaffService service, CancellationToken ct) => Results.Ok(await service.ListAsync(UserId(principal), page, pageSize == 0 ? 25 : pageSize, search, status, ct));
    private static async Task<IResult> GetStaffAsync(Guid staffId, ClaimsPrincipal principal, IStaffService service, CancellationToken ct) => Results.Ok(await service.GetAsync(UserId(principal), staffId, ct));
    private static async Task<IResult> CreateStaffAsync(StaffInput input, ClaimsPrincipal principal, IStaffService service, IAuditWriter audit, CancellationToken ct) => await CreatedHrAsync(await service.CreateAsync(UserId(principal), input, ct), "StaffProfile", "Staff.Create", principal, audit, ct);
    private static async Task<IResult> UpdateStaffAsync(Guid staffId, StaffInput input, ClaimsPrincipal principal, IStaffService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpdateAsync(actor, staffId, input, ct); await audit.WriteAsync(actor, "Staff.Update", "StaffProfile", staffId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> SetStaffStatusAsync(Guid staffId, StaffStatusInput input, ClaimsPrincipal principal, IStaffService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.SetStatusAsync(actor, staffId, input, ct); await audit.WriteAsync(actor, "Staff.StatusChange", "StaffProfile", staffId.ToString(), "Succeeded", JsonSerializer.Serialize(new { input.Status }), ct); return Results.NoContent(); }
    private static async Task<IResult> LinkStaffUserAsync(Guid staffId, StaffUserLinkInput input, ClaimsPrincipal principal, IStaffService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.LinkUserAsync(actor, staffId, input, ct); await audit.WriteAsync(actor, "Staff.LinkUser", "StaffProfile", staffId.ToString(), "Succeeded", JsonSerializer.Serialize(new { input.UserId }), ct); return Results.NoContent(); }
    private static async Task<IResult> GetStaffSensitiveAsync(Guid staffId, ClaimsPrincipal principal, IStaffService service, CancellationToken ct)
    { var result = await service.GetSensitiveAsync(UserId(principal), staffId, ct); return result is null ? Results.NotFound() : Results.Ok(result); }
    private static async Task<IResult> UpsertStaffSensitiveAsync(Guid staffId, StaffSensitiveInput input, ClaimsPrincipal principal, IStaffService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpsertSensitiveAsync(actor, staffId, input, ct); await audit.WriteAsync(actor, "Staff.SensitiveUpdate", "StaffProfile", staffId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> ListTeachingAssignmentsAsync(Guid? classSectionId, ClaimsPrincipal principal, IStaffService service, CancellationToken ct) => Results.Ok(await service.ListTeachingAssignmentsAsync(UserId(principal), classSectionId, ct));
    private static async Task<IResult> CreateTeachingAssignmentAsync(TeachingAssignmentInput input, ClaimsPrincipal principal, IStaffService service, IAuditWriter audit, CancellationToken ct) => await CreatedHrAsync(await service.CreateTeachingAssignmentAsync(UserId(principal), input, ct), "TeachingAssignment", "TeachingAssignment.Create", principal, audit, ct);
    private static async Task<IResult> DeleteTeachingAssignmentAsync(Guid assignmentId, ClaimsPrincipal principal, IStaffService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.DeleteTeachingAssignmentAsync(actor, assignmentId, ct); await audit.WriteAsync(actor, "TeachingAssignment.Delete", "TeachingAssignment", assignmentId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }

    private static async Task<IResult> ListApplicantsAsync(int page, int pageSize, ApplicationStatus? status, ClaimsPrincipal principal, IStudentLifecycleService service, CancellationToken ct) => Results.Ok(await service.ListApplicantsAsync(UserId(principal), page, pageSize == 0 ? 25 : pageSize, status, ct));
    private static async Task<IResult> CreateApplicantAsync(ApplicantInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct) => await CreatedLifecycleAsync(await service.CreateApplicantAsync(UserId(principal), input, ct), "Applicant", "Applicant.Create", principal, audit, ct);
    private static async Task<IResult> TransitionApplicantAsync(Guid applicantId, ApplicationTransitionInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.TransitionAsync(actor, applicantId, input, ct); await audit.WriteAsync(actor, "Applicant.StatusChange", "Applicant", applicantId.ToString(), "Succeeded", JsonSerializer.Serialize(new { input.Status }), ct); return Results.NoContent(); }
    private static async Task<IResult> GetApplicantSensitiveAsync(Guid applicantId, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var value = await service.GetApplicantSensitiveAsync(actor, applicantId, ct); await audit.WriteAsync(actor, "Applicant.SensitiveRead", "Applicant", applicantId.ToString(), "Succeeded", null, ct); return value is null ? Results.NotFound() : Results.Ok(value); }
    private static async Task<IResult> UpsertApplicantSensitiveAsync(Guid applicantId, ApplicantSensitiveInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpsertApplicantSensitiveAsync(actor, applicantId, input, ct); await audit.WriteAsync(actor, "Applicant.SensitiveUpdate", "Applicant", applicantId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> GetAdmissionReviewAsync(Guid applicantId, ClaimsPrincipal principal, IStudentLifecycleService service, CancellationToken ct)
    { var value = await service.GetAdmissionReviewAsync(UserId(principal), applicantId, ct); return value is null ? Results.NotFound() : Results.Ok(value); }
    private static async Task<IResult> UpsertAdmissionReviewAsync(Guid applicantId, AdmissionReviewInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpsertAdmissionReviewAsync(actor, applicantId, input, ct); await audit.WriteAsync(actor, "Applicant.Review", "Applicant", applicantId.ToString(), "Succeeded", JsonSerializer.Serialize(new { input.Score }), ct); return Results.NoContent(); }
    private static async Task<IResult> ListAdmissionInterviewsAsync(Guid applicantId, ClaimsPrincipal principal, IStudentLifecycleService service, CancellationToken ct) => Results.Ok(await service.ListAdmissionInterviewsAsync(UserId(principal), applicantId, ct));
    private static async Task<IResult> ScheduleAdmissionInterviewAsync(Guid applicantId, AdmissionInterviewInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var id = await service.ScheduleAdmissionInterviewAsync(actor, applicantId, input, ct); await audit.WriteAsync(actor, "Applicant.InterviewSchedule", "AdmissionInterview", id.ToString(), "Succeeded", JsonSerializer.Serialize(new { applicantId, input.ScheduledAtUtc }), ct); return Results.Created($"/api/v1/admissions/interviews/{id}", new { id }); }
    private static async Task<IResult> CompleteAdmissionInterviewAsync(Guid interviewId, AdmissionInterviewOutcomeInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.CompleteAdmissionInterviewAsync(actor, interviewId, input, ct); await audit.WriteAsync(actor, "Applicant.InterviewOutcome", "AdmissionInterview", interviewId.ToString(), "Succeeded", JsonSerializer.Serialize(new { input.Status }), ct); return Results.NoContent(); }
    private static async Task<IResult> ConvertApplicantAsync(Guid applicantId, ConvertApplicantInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var id = await service.ConvertAsync(actor, applicantId, input, ct); await audit.WriteAsync(actor, "Applicant.Convert", "Student", id.ToString(), "Succeeded", JsonSerializer.Serialize(new { applicantId }), ct); return Results.Created($"/api/v1/students/{id}", new { id }); }
    private static async Task<IResult> SendApplicantCommunicationAsync(Guid applicantId, ApplicantCommunicationInput input, ClaimsPrincipal principal, IAdmissionsCommunicationService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var id = await service.SendAsync(actor, applicantId, input, ct); await audit.WriteAsync(actor, "Applicant.CommunicationQueue", "NotificationMessage", id.ToString(), "Succeeded", JsonSerializer.Serialize(new { applicantId, input.Type }), ct); return Results.Accepted($"/api/v1/notifications/{id}", new { id }); }
    private static async Task<IResult> ListStudentsAsync(int page, int pageSize, string? search, ClaimsPrincipal principal, IStudentLifecycleService service, CancellationToken ct) => Results.Ok(await service.ListStudentsAsync(UserId(principal), page, pageSize == 0 ? 25 : pageSize, search, ct));
    private static async Task<IResult> GetStudentAsync(Guid studentId, ClaimsPrincipal principal, IStudentLifecycleService service, CancellationToken ct) => Results.Ok(await service.GetStudentAsync(UserId(principal), studentId, ct));
    private static async Task<IResult> UpdateStudentAsync(Guid studentId, StudentProfileInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpdateStudentAsync(actor, studentId, input, ct); await audit.WriteAsync(actor, "Student.Update", "Student", studentId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> EnrollStudentAsync(Guid studentId, EnrollmentInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var id = await service.EnrollStudentAsync(actor, studentId, input, ct); await audit.WriteAsync(actor, "Student.Enroll", "Enrollment", id.ToString(), "Succeeded", JsonSerializer.Serialize(new { studentId, input.AcademicYearId, input.ClassSectionId }), ct); return Results.Created($"/api/v1/students/{studentId}/enrollments/{id}", new { id }); }
    private static async Task<IResult> WithdrawStudentAsync(Guid studentId, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.WithdrawStudentAsync(actor, studentId, ct); await audit.WriteAsync(actor, "Student.Withdraw", "Student", studentId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> GetStudentSensitiveAsync(Guid studentId, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); var value = await service.GetStudentSensitiveAsync(actor, studentId, ct); await audit.WriteAsync(actor, "Student.SensitiveRead", "Student", studentId.ToString(), "Succeeded", null, ct); return value is null ? Results.NotFound() : Results.Ok(value); }
    private static async Task<IResult> UpsertStudentSensitiveAsync(Guid studentId, StudentSensitiveInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpsertStudentSensitiveAsync(actor, studentId, input, ct); await audit.WriteAsync(actor, "Student.SensitiveUpdate", "Student", studentId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> CreateGuardianAsync(GuardianInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct) => await CreatedLifecycleAsync(await service.CreateGuardianAsync(UserId(principal), input, ct), "Guardian", "Guardian.Create", principal, audit, ct);
    private static async Task<IResult> ListGuardiansAsync(int page, int pageSize, string? search, ClaimsPrincipal principal, IStudentLifecycleService service, CancellationToken ct) => Results.Ok(await service.ListGuardiansAsync(UserId(principal), page, pageSize == 0 ? 25 : pageSize, search, ct));
    private static async Task<IResult> GetGuardianAsync(Guid guardianId, ClaimsPrincipal principal, IStudentLifecycleService service, CancellationToken ct) => Results.Ok(await service.GetGuardianAsync(UserId(principal), guardianId, ct));
    private static async Task<IResult> UpdateGuardianAsync(Guid guardianId, GuardianInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.UpdateGuardianAsync(actor, guardianId, input, ct); await audit.WriteAsync(actor, "Guardian.Update", "Guardian", guardianId.ToString(), "Succeeded", null, ct); return Results.NoContent(); }
    private static async Task<IResult> LinkGuardianAsync(Guid studentId, GuardianLinkInput input, ClaimsPrincipal principal, IStudentLifecycleService service, IAuditWriter audit, CancellationToken ct)
    { var actor = UserId(principal); await service.LinkGuardianAsync(actor, studentId, input, ct); await audit.WriteAsync(actor, "Student.GuardianLink", "Student", studentId.ToString(), "Succeeded", JsonSerializer.Serialize(new { input.GuardianId }), ct); return Results.NoContent(); }

    private static async Task<IResult> CreatedAsync(Guid id, string targetType, string action, ClaimsPrincipal principal, IAuditWriter audit, CancellationToken ct)
    { await audit.WriteAsync(UserId(principal), action, targetType, id.ToString(), "Succeeded", null, ct); return Results.Created($"/api/v1/academics/{targetType.ToLowerInvariant()}/{id}", new { id }); }
    private static async Task<IResult> CreatedHrAsync(Guid id, string targetType, string action, ClaimsPrincipal principal, IAuditWriter audit, CancellationToken ct)
    { await audit.WriteAsync(UserId(principal), action, targetType, id.ToString(), "Succeeded", null, ct); return Results.Created($"/api/v1/hr/{targetType.ToLowerInvariant()}/{id}", new { id }); }
    private static async Task<IResult> CreatedLifecycleAsync(Guid id, string targetType, string action, ClaimsPrincipal principal, IAuditWriter audit, CancellationToken ct)
    { await audit.WriteAsync(UserId(principal), action, targetType, id.ToString(), "Succeeded", null, ct); return Results.Created($"/api/v1/{targetType.ToLowerInvariant()}s/{id}", new { id }); }
    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
}
