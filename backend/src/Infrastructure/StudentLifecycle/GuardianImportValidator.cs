using System.Net.Mail;
using GiddyEdu.Infrastructure.Persistence;
using GiddyEdu.Modules.StudentLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace GiddyEdu.Infrastructure.StudentLifecycle;

internal sealed record GuardianImportRow(int Row, string AdmissionNumber, string FirstName, string LastName, string Phone,
    string Email, string Address, GuardianRelationshipType Relationship, bool IsPrimary, bool IsEmergencyContact,
    bool MayCollect, Guid? StudentId, Guid? ExistingGuardianId, string? StudentName, string? ClassName, string? Error);

internal static class GuardianImportValidator
{
    internal static async Task<IReadOnlyList<GuardianImportRow>> ValidateAsync(GiddyEduDbContext db,
        IReadOnlyList<IReadOnlyList<string>> rows, Guid campusId, CancellationToken ct)
    {
        if (rows.Count == 0 || !rows[0].SequenceEqual(GuardianImportFile.Headers, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"The header must be: {string.Join(',', GuardianImportFile.Headers)}.");
        var data = rows.Skip(1).Where(row => row.Any(value => !string.IsNullOrWhiteSpace(value))).ToArray();
        if (data.Length == 0) throw new ArgumentException("The file contains no guardian relationships.");
        if (data.Length > GuardianImportFile.MaximumRows)
            throw new ArgumentException($"Upload at most {GuardianImportFile.MaximumRows} relationships per file.");

        var numbers = data.Where(row => row.Count > 0).Select(row => row[0].Trim().ToUpperInvariant()).Distinct().ToArray();
        var students = await (from enrollment in db.Enrollments.AsNoTracking()
                              join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
                              join section in db.ClassSections.AsNoTracking() on enrollment.ClassSectionId equals section.Id
                              where section.CampusId == campusId && enrollment.Status == EnrollmentStatus.Active
                                  && student.Status == StudentStatus.Active && numbers.Contains(student.AdmissionNumber.ToUpper())
                              select new { student.Id, student.AdmissionNumber, student.FirstName, student.LastName,
                                  SectionName = section.Name, enrollment.EnrolledOn }).ToListAsync(ct);
        var studentByNumber = students.GroupBy(x => x.AdmissionNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.EnrolledOn).First(), StringComparer.OrdinalIgnoreCase);

        var phones = data.Where(row => row.Count > 3).Select(row => row[3].Trim()).Distinct().ToArray();
        var emails = data.Where(row => row.Count > 4).Select(row => row[4].Trim().ToLowerInvariant()).Distinct().ToArray();
        var existing = await db.Guardians.IgnoreQueryFilters(["BinFilter"]).AsNoTracking()
            .Where(x => phones.Contains(x.Phone) || x.Email != null && emails.Contains(x.Email.ToLower()))
            .ToListAsync(ct);
        var studentIds = students.Select(x => x.Id).Distinct().ToArray();
        var guardianIds = existing.Select(x => x.Id).ToArray();
        var existingLinks = await db.StudentGuardians.AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && guardianIds.Contains(x.GuardianId))
            .Select(x => new { x.StudentId, x.GuardianId }).ToListAsync(ct);
        var linked = existingLinks.Select(x => (x.StudentId, x.GuardianId)).ToHashSet();
        var primaryStudents = (await db.StudentGuardians.AsNoTracking()
            .Where(x => studentIds.Contains(x.StudentId) && x.IsPrimary).Select(x => x.StudentId).ToListAsync(ct)).ToHashSet();
        var seenContacts = new Dictionary<string, (string First, string Last, string Email, string Address)>(StringComparer.Ordinal);
        var seenEmails = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenLinks = new HashSet<(Guid StudentId, string Phone)>();
        var reviewed = new List<GuardianImportRow>(data.Length);

        for (var index = 0; index < data.Length; index++)
        {
            var values = data[index];
            var number = values.ElementAtOrDefault(0)?.Trim().ToUpperInvariant() ?? "";
            var first = values.ElementAtOrDefault(1)?.Trim() ?? "";
            var last = values.ElementAtOrDefault(2)?.Trim() ?? "";
            var phone = values.ElementAtOrDefault(3)?.Trim() ?? "";
            var email = values.ElementAtOrDefault(4)?.Trim().ToLowerInvariant() ?? "";
            var address = values.ElementAtOrDefault(5)?.Trim() ?? "";
            var relationshipText = values.ElementAtOrDefault(6)?.Trim() ?? "";
            var issues = new List<string>();
            if (values.Count != GuardianImportFile.Headers.Length) issues.Add("Use the ten columns in the template.");
            if (string.IsNullOrWhiteSpace(first) || first.Length > 100 || string.IsNullOrWhiteSpace(last) || last.Length > 100)
                issues.Add("First and last name are required and must be at most 100 characters.");
            if (phone.Length != 11 || phone.Any(value => value is < '0' or > '9')) issues.Add("Phone must contain 11 digits.");
            if (email.Length > 320 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
                issues.Add("A valid email is required.");
            if (address.Length is < 1 or > 1000) issues.Add("Address is required and must be at most 1000 characters.");
            if (!Enum.TryParse<GuardianRelationshipType>(relationshipText, true, out var relationship)
                || !Enum.IsDefined(relationship) || int.TryParse(relationshipText, out _))
                issues.Add("Choose a relationship from the template keys.");
            var primary = ParseFlag(values.ElementAtOrDefault(7), "PrimaryGuardian", issues);
            var emergency = ParseFlag(values.ElementAtOrDefault(8), "EmergencyContact", issues);
            var collect = ParseFlag(values.ElementAtOrDefault(9), "MayCollect", issues);
            if (!studentByNumber.TryGetValue(number, out var student))
                issues.Add("No active student with this admission number is enrolled in the selected campus.");

            var phoneMatches = existing.Where(x => x.Phone == phone).ToArray();
            var emailMatches = existing.Where(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (phoneMatches.Length > 1 || emailMatches.Length > 1) issues.Add("Multiple existing guardians match these details. Resolve the duplicates first.");
            var existingGuardian = phoneMatches.FirstOrDefault();
            if (existingGuardian is null && emailMatches.Length == 1 || existingGuardian is not null &&
                (emailMatches.Length != 1 || emailMatches[0].Id != existingGuardian.Id))
                issues.Add("Email or phone belongs to a different guardian. Review the existing record.");
            if (existingGuardian?.DeletedAtUtc is not null) issues.Add("This guardian is in the bin. Restore the record before importing.");
            if (existingGuardian is not null && (!string.Equals(existingGuardian.FirstName, first, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(existingGuardian.LastName, last, StringComparison.OrdinalIgnoreCase)
                    || existingGuardian.Address is not null && !string.Equals(existingGuardian.Address, address, StringComparison.OrdinalIgnoreCase)))
                issues.Add("The guardian's name or address conflicts with the existing record.");
            if (seenContacts.TryGetValue(phone, out var previous)
                && (!string.Equals(previous.First, first, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(previous.Last, last, StringComparison.OrdinalIgnoreCase)
                    || previous.Email != email || !string.Equals(previous.Address, address, StringComparison.OrdinalIgnoreCase)))
                issues.Add("This phone is used with different guardian details elsewhere in the file.");
            if (seenEmails.TryGetValue(email, out var previousPhone) && previousPhone != phone)
                issues.Add("This email is used with another phone number elsewhere in the file.");
            if (student is not null)
            {
                if (existingGuardian is not null && linked.Contains((student.Id, existingGuardian.Id)))
                    issues.Add("This guardian is already linked to the student.");
                if (seenLinks.Contains((student.Id, phone))) issues.Add("This guardian-student link appears more than once in the file.");
                if (primary && primaryStudents.Contains(student.Id)) issues.Add("The student already has a primary guardian.");
            }

            var error = issues.Count == 0 ? null : string.Join(" ", issues);
            reviewed.Add(new GuardianImportRow(index + 2, number, first, last, phone, email, address,
                relationship, primary, emergency, collect, student?.Id, existingGuardian?.Id,
                student is null ? null : $"{student.FirstName} {student.LastName}", student?.SectionName, error));
            if (error is not null) continue;
            seenContacts.TryAdd(phone, (first, last, email, address));
            seenEmails.TryAdd(email, phone);
            seenLinks.Add((student!.Id, phone));
            if (primary) primaryStudents.Add(student.Id);
        }
        return reviewed;
    }

    private static bool ParseFlag(string? value, string name, ICollection<string> issues)
    {
        if (string.Equals(value?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(value?.Trim(), "No", StringComparison.OrdinalIgnoreCase)) return false;
        issues.Add($"{name} must be Yes or No.");
        return false;
    }
}
