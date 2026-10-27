using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Ardak.Model.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace landy.Controllers;

/// <summary>
/// Manages legal and administrative procedures linked to property records.
/// Only SuperAdmin and Admin may access this controller.
/// Internal notes and financial details are never surfaced to customers.
/// </summary>
[Authorize(Policy = AppPolicies.RequireAdminOrSuperAdmin)]
public class ProceduresController : Controller
{
    private readonly IProcedureService _procedureService;
    private readonly IPropertyService _propertyService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ProceduresController> _logger;

    public ProceduresController(
        IProcedureService procedureService,
        IPropertyService propertyService,
        ICurrentUserService currentUserService,
        ILogger<ProceduresController> logger)
    {
        _procedureService = procedureService;
        _propertyService = propertyService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    // GET /Procedures  [with optional filters]
    [HttpGet]
    public async Task<IActionResult> Index(ProcedureSearchFilterDto filter)
    {
        var procedures = await _procedureService.SearchProceduresAsync(filter);

        ViewBag.Filter = filter;
        BuildFilterDropdowns(filter.Type, filter.Status);

        return View(procedures);
    }

    // GET /Procedures/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var procedure = await _procedureService.GetProcedureByIdAsync(id);
        if (procedure == null)
            return NotFound("Procedure record not found.");

        return View(procedure);
    }

    // GET /Procedures/Create?propertyId=3
    [HttpGet]
    public async Task<IActionResult> Create(int? propertyId)
    {
        await PopulatePropertyDropdownAsync(propertyId);

        var dto = new CreateProcedureDto
        {
            PropertyId = propertyId ?? 0,
            SubmissionDate = DateTime.Today
        };

        return View(dto);
    }

    // POST /Procedures/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateProcedureDto model)
    {
        if (!ModelState.IsValid)
        {
            await PopulatePropertyDropdownAsync(model.PropertyId);
            return View(model);
        }

        var result = await _procedureService.CreateProcedureAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم تسجيل المعاملة بنجاح.";
            return RedirectToAction(nameof(Details), new { id = result.Data });
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "Failed to create procedure.");
        await PopulatePropertyDropdownAsync(model.PropertyId);
        return View(model);
    }

    // GET /Procedures/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var procedure = await _procedureService.GetProcedureByIdAsync(id);
        if (procedure == null)
            return NotFound("Procedure record not found.");

        await PopulatePropertyDropdownAsync(procedure.PropertyId);

        var dto = new UpdateProcedureDto
        {
            Id = procedure.Id,
            PropertyId = procedure.PropertyId,
            Type = procedure.Type,
            TypeLabel = procedure.TypeLabel,
            ApplicationNumber = procedure.ApplicationNumber,
            ResponsibleAuthority = procedure.ResponsibleAuthority,
            Status = procedure.Status,
            SubmissionDate = procedure.SubmissionDate,
            NextActionDeadline = procedure.NextActionDeadline,
            NextActionDescription = procedure.NextActionDescription,
            MissingDocuments = procedure.MissingDocuments,
            TotalFee = procedure.TotalFee,
            AmountPaid = procedure.AmountPaid,
            InternalNotes = procedure.InternalNotes,
            IsCustomerVisible = procedure.IsCustomerVisible,
            CustomerUpdateNotes = procedure.CustomerUpdateNotes
        };

        return View(dto);
    }

    // POST /Procedures/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateProcedureDto model)
    {
        if (!ModelState.IsValid)
        {
            await PopulatePropertyDropdownAsync(model.PropertyId);
            return View(model);
        }

        var result = await _procedureService.UpdateProcedureAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم تحديث بيانات المعاملة بنجاح.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "Failed to update procedure.");
        await PopulatePropertyDropdownAsync(model.PropertyId);
        return View(model);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private async Task PopulatePropertyDropdownAsync(int? selectedId)
    {
        var properties = await _propertyService.SearchPropertiesAsync(new PropertySearchFilterDto());
        ViewBag.Properties = new SelectList(
            properties.Select(p => new { p.Id, Label = $"{p.ReferenceNumber} – {p.Title}" }),
            "Id", "Label", selectedId);
    }

    private void BuildFilterDropdowns(ProcedureType? type, ProcedureStatus? status)
    {
        ViewBag.ProcedureTypes = new SelectList(
            Enum.GetValues<ProcedureType>().Select(t => new { Value = (int)t, Text = t.ToString() }),
            "Value", "Text", type.HasValue ? (int)type.Value : (object?)null);

        ViewBag.ProcedureStatuses = new SelectList(
            Enum.GetValues<ProcedureStatus>().Select(s => new { Value = (int)s, Text = s.ToString() }),
            "Value", "Text", status.HasValue ? (int)status.Value : (object?)null);
    }
}
