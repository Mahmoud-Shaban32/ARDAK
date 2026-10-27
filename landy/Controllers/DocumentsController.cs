using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Ardak.Model.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace landy.Controllers;

/// <summary>
/// Secure document management controller.
/// Enforces server-side authorization on all operations.
/// Physical storage paths, storage credentials, or internal file locations are never exposed.
/// </summary>
[Authorize(Policy = AppPolicies.RequireAdminOrSuperAdmin)]
public class DocumentsController : Controller
{
    private readonly IDocumentService _documentService;
    private readonly ICustomerService _customerService;
    private readonly IPropertyService _propertyService;
    private readonly IProcedureService _procedureService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService documentService,
        ICustomerService customerService,
        IPropertyService propertyService,
        IProcedureService procedureService,
        ICurrentUserService currentUserService,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _customerService = customerService;
        _propertyService = propertyService;
        _procedureService = procedureService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    // GET: /Documents
    [HttpGet]
    public async Task<IActionResult> Index(DocumentSearchFilterDto filter)
    {
        var documents = await _documentService.SearchDocumentsAsync(filter, _currentUserService.UserId!);

        ViewBag.Filter = filter;
        await PopulateFilterDropdownsAsync(filter);

        return View(documents);
    }

    // GET: /Documents/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var document = await _documentService.GetDocumentByIdAsync(id, _currentUserService.UserId!);
        if (document == null)
        {
            return NotFound("Document record not found or access denied.");
        }

        return View(document);
    }

    // GET: /Documents/Upload
    [HttpGet]
    public async Task<IActionResult> Upload(int? customerId, int? propertyId, int? procedureId)
    {
        await PopulateAssociationDropdownsAsync(customerId, propertyId, procedureId);

        var dto = new UploadDocumentDto
        {
            CustomerId = customerId,
            PropertyId = propertyId,
            ProcedureId = procedureId,
            ConfidentialityLevel = DocumentConfidentialityLevel.Internal,
            IsCustomerVisible = false // Secure default: strictly private
        };

        return View(dto);
    }

    // POST: /Documents/Upload
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(UploadDocumentDto model, IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Please select a file to upload.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAssociationDropdownsAsync(model.CustomerId, model.PropertyId, model.ProcedureId);
            return View(model);
        }

        using var stream = file!.OpenReadStream();
        var result = await _documentService.UploadDocumentAsync(
            model,
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            _currentUserService.UserId!);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم رفع المستند وأرشفته بأمان بنجاح.";
            return RedirectToAction(nameof(Details), new { id = result.Data });
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "Failed to upload document.");
        await PopulateAssociationDropdownsAsync(model.CustomerId, model.PropertyId, model.ProcedureId);
        return View(model);
    }

    // GET: /Documents/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var doc = await _documentService.GetDocumentByIdAsync(id, _currentUserService.UserId!);
        if (doc == null)
        {
            return NotFound("Document record not found.");
        }

        var dto = new UpdateDocumentMetadataDto
        {
            Id = doc.Id,
            Type = doc.Type,
            TypeLabel = doc.TypeLabel,
            Title = doc.Title,
            Description = doc.Description,
            ReferenceNumber = doc.ReferenceNumber,
            ConfidentialityLevel = doc.ConfidentialityLevel,
            IsCustomerVisible = doc.IsCustomerVisible,
            InternalNotes = doc.InternalNotes
        };

        ViewBag.Document = doc;
        return View(dto);
    }

    // POST: /Documents/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateDocumentMetadataDto model)
    {
        if (!ModelState.IsValid)
        {
            var doc = await _documentService.GetDocumentByIdAsync(model.Id, _currentUserService.UserId!);
            ViewBag.Document = doc;
            return View(model);
        }

        var result = await _documentService.UpdateDocumentMetadataAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم تحديث بيانات وتصنيف المستند بنجاح.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "Failed to update document.");
        var currentDoc = await _documentService.GetDocumentByIdAsync(model.Id, _currentUserService.UserId!);
        ViewBag.Document = currentDoc;
        return View(model);
    }

    // POST: /Documents/Archive/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var result = await _documentService.ArchiveDocumentAsync(id, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تمت أرشفة المستند بنجاح مع الاحتفاظ بالسجل التاريخي.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "فشلت عملية أرشفة المستند.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: /Documents/Download/5
    // Requires server-side authorization check before streaming
    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var result = await _documentService.GetDocumentDownloadAsync(id, _currentUserService.UserId!);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Download denied for document ID {DocumentId} to user '{UserId}'.",
                id, _currentUserService.UserId);
            return Forbid();
        }

        var download = result.Data!;
        // Stream directly to client with original filename and content type
        // Physical storage path is completely shielded
        return File(download.Stream, download.ContentType, download.DownloadFileName);
    }

    // ── Helper Dropdowns ─────────────────────────────────────────────────────

    private async Task PopulateFilterDropdownsAsync(DocumentSearchFilterDto filter)
    {
        ViewBag.DocumentTypes = new SelectList(
            Enum.GetValues<DocumentType>().Select(t => new { Value = (int)t, Text = t.ToString() }),
            "Value", "Text", filter.Type.HasValue ? (int)filter.Type.Value : (object?)null);

        ViewBag.ConfidentialityLevels = new SelectList(
            Enum.GetValues<DocumentConfidentialityLevel>().Select(c => new { Value = (int)c, Text = c.ToString() }),
            "Value", "Text", filter.ConfidentialityLevel.HasValue ? (int)filter.ConfidentialityLevel.Value : (object?)null);
    }

    private async Task PopulateAssociationDropdownsAsync(int? selectedCustomer, int? selectedProperty, int? selectedProcedure)
    {
        var customers = await _customerService.SearchCustomersAsync(string.Empty);
        ViewBag.Customers = new SelectList(
            customers.Select(c => new { c.Id, Label = $"{c.FullName} (#{c.Id})" }),
            "Id", "Label", selectedCustomer);

        var properties = await _propertyService.SearchPropertiesAsync(new PropertySearchFilterDto());
        ViewBag.Properties = new SelectList(
            properties.Select(p => new { p.Id, Label = $"{p.ReferenceNumber} – {p.Title}" }),
            "Id", "Label", selectedProperty);

        var procedures = await _procedureService.SearchProceduresAsync(new ProcedureSearchFilterDto());
        ViewBag.Procedures = new SelectList(
            procedures.Select(pr => new { pr.Id, Label = $"[#{pr.Id}] {pr.TypeLabel} ({pr.PropertyReferenceNumber})" }),
            "Id", "Label", selectedProcedure);

        ViewBag.DocumentTypes = new SelectList(
            Enum.GetValues<DocumentType>().Select(t => new { Value = (int)t, Text = t.ToString() }),
            "Value", "Text");

        ViewBag.ConfidentialityLevels = new SelectList(
            Enum.GetValues<DocumentConfidentialityLevel>().Select(c => new { Value = (int)c, Text = c.ToString() }),
            "Value", "Text", (int)DocumentConfidentialityLevel.Internal);
    }
}
