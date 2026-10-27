using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ardak.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ArdakDbContext _db;
    private readonly ILogger<DashboardService> _logger;

    private static readonly ProcedureStatus[] PendingStatuses =
    [
        ProcedureStatus.Submitted,
        ProcedureStatus.InProgress,
        ProcedureStatus.PendingRequirements,
        ProcedureStatus.UnderReview
    ];

    public DashboardService(ArdakDbContext db, ILogger<DashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AdminDashboardDto> GetDashboardMetricsAsync(int topItems = 8)
    {
        var today = DateTime.UtcNow.Date;

        // 1. High-level counts via direct database COUNT queries (sequential to avoid DbContext concurrency)
        var totalCustomers = await _db.Customers.CountAsync();
        var activeCustomers = await _db.Customers.CountAsync(c => c.Status == CustomerStatus.Active);

        var totalProperties = await _db.Properties.CountAsync();
        var activeProperties = await _db.Properties.CountAsync(p => p.Status == PropertyStatus.Active);

        var totalProcedures = await _db.Procedures.CountAsync();
        var totalDocuments = await _db.Documents.CountAsync(d => !d.IsArchived);

        var pendingCount = await _db.Procedures.CountAsync(p => PendingStatuses.Contains(p.Status));
        var missingDocsCount = await _db.Procedures.CountAsync(p =>
            p.MissingDocuments != null &&
            p.MissingDocuments != "" &&
            p.Status != ProcedureStatus.Completed &&
            p.Status != ProcedureStatus.Cancelled);

        var overdueCount = await _db.Procedures.CountAsync(p =>
            p.NextActionDeadline.HasValue &&
            p.NextActionDeadline.Value.Date < today &&
            p.Status != ProcedureStatus.Completed &&
            p.Status != ProcedureStatus.Cancelled);

        // 2. Groupings via database GROUP BY queries
        var propertiesByType = await _db.Properties
            .GroupBy(p => p.Type)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var propertiesByStatus = await _db.Properties
            .GroupBy(p => p.Status)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var proceduresByStatus = await _db.Procedures
            .GroupBy(p => p.Status)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        // 3. Actionable Procedure Lists (projected directly and limited with Take)
        var pendingProcedures = await _db.Procedures
            .AsNoTracking()
            .Include(p => p.Property)
            .Where(p => PendingStatuses.Contains(p.Status))
            .OrderBy(p => p.NextActionDeadline.HasValue ? 0 : 1)
            .ThenBy(p => p.NextActionDeadline)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Take(topItems)
            .Select(p => MapToSummaryDto(p))
            .ToListAsync();

        var missingDocsProcedures = await _db.Procedures
            .AsNoTracking()
            .Include(p => p.Property)
            .Where(p =>
                p.MissingDocuments != null &&
                p.MissingDocuments != "" &&
                p.Status != ProcedureStatus.Completed &&
                p.Status != ProcedureStatus.Cancelled)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(topItems)
            .Select(p => MapToSummaryDto(p))
            .ToListAsync();

        var upcomingDeadlines = await _db.Procedures
            .AsNoTracking()
            .Include(p => p.Property)
            .Where(p =>
                p.NextActionDeadline.HasValue &&
                p.Status != ProcedureStatus.Completed &&
                p.Status != ProcedureStatus.Cancelled)
            .OrderBy(p => p.NextActionDeadline)
            .Take(topItems)
            .Select(p => MapToSummaryDto(p))
            .ToListAsync();

        // 4. Recent Activities (recent status history & recent documents)
        var recentHistory = await _db.ProcedureStatusHistories
            .AsNoTracking()
            .Include(h => h.Procedure)
                .ThenInclude(pr => pr!.Property)
            .OrderByDescending(h => h.ChangedAtUtc)
            .Take(topItems)
            .ToListAsync();

        var recentDocs = await _db.Documents
            .AsNoTracking()
            .Include(d => d.Property)
            .Include(d => d.Customer)
            .OrderByDescending(d => d.UploadedAtUtc)
            .Take(topItems)
            .ToListAsync();

        // Combine and sort recent activities
        var activities = new List<DashboardActivityDto>();

        foreach (var h in recentHistory)
        {
            activities.Add(new DashboardActivityDto
            {
                ActivityType = "ProcedureStatusChange",
                Title = $"Procedure #{h.ProcedureId} ({h.Procedure?.TypeLabel ?? h.Procedure?.Type.ToString()}) status changed",
                Description = $"Transitioned from {h.FromStatus} to {h.ToStatus}. {(string.IsNullOrWhiteSpace(h.ChangeNote) ? string.Empty : $"Note: {h.ChangeNote}")}",
                PerformedByUserId = h.ChangedByUserId,
                TimestampUtc = h.ChangedAtUtc,
                LinkController = "Procedures",
                LinkAction = "Details",
                LinkId = h.ProcedureId,
                BadgeCss = "bg-primary"
            });
        }

        foreach (var d in recentDocs)
        {
            var target = d.Property != null ? $"Property {d.Property.ReferenceNumber}" : (d.Customer != null ? $"Customer {d.Customer.FullName}" : "Repository");
            activities.Add(new DashboardActivityDto
            {
                ActivityType = "DocumentUploaded",
                Title = $"Document uploaded: {d.Title}",
                Description = $"Uploaded '{d.OriginalFileName}' for {target}.",
                PerformedByUserId = d.UploadedByUserId,
                TimestampUtc = d.UploadedAtUtc,
                LinkController = "Documents",
                LinkAction = "Details",
                LinkId = d.Id,
                BadgeCss = "bg-success"
            });
        }

        var sortedActivities = activities
            .OrderByDescending(a => a.TimestampUtc)
            .Take(topItems)
            .ToList();

        return new AdminDashboardDto
        {
            TotalCustomers = totalCustomers,
            ActiveCustomers = activeCustomers,
            TotalProperties = totalProperties,
            ActiveProperties = activeProperties,
            TotalProcedures = totalProcedures,
            PendingProceduresCount = pendingCount,
            ProceduresWithMissingDocsCount = missingDocsCount,
            OverdueDeadlinesCount = overdueCount,
            TotalDocuments = totalDocuments,
            PropertiesByType = propertiesByType,
            PropertiesByStatus = propertiesByStatus,
            ProceduresByStatus = proceduresByStatus,
            PendingProcedures = pendingProcedures,
            ProceduresWithMissingDocuments = missingDocsProcedures,
            UpcomingDeadlines = upcomingDeadlines,
            RecentActivities = sortedActivities
        };
    }

    private static ProcedureSummaryDto MapToSummaryDto(Procedure p) => new()
    {
        Id = p.Id,
        PropertyId = p.PropertyId,
        PropertyReferenceNumber = p.Property != null ? p.Property.ReferenceNumber : string.Empty,
        PropertyTitle = p.Property != null ? p.Property.Title : string.Empty,
        Type = p.Type,
        TypeLabel = p.TypeLabel ?? p.Type.ToString(),
        ApplicationNumber = p.ApplicationNumber,
        ResponsibleAuthority = p.ResponsibleAuthority,
        Status = p.Status,
        SubmissionDate = p.SubmissionDate,
        NextActionDeadline = p.NextActionDeadline,
        NextActionDescription = p.NextActionDescription,
        CreatedAtUtc = p.CreatedAtUtc
    };
}
