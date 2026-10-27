using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Ardak.Model.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace landy.Controllers;

[Authorize(Policy = AppPolicies.RequireAdminOrSuperAdmin)]
public class PropertiesController : Controller
{
    private readonly IPropertyService _propertyService;
    private readonly ICustomerService _customerService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IProcedureService _procedureService;
    private readonly ILogger<PropertiesController> _logger;

    public PropertiesController(
        IPropertyService propertyService,
        ICustomerService customerService,
        ICurrentUserService currentUserService,
        IProcedureService procedureService,
        ILogger<PropertiesController> logger)
    {
        _propertyService = propertyService;
        _customerService = customerService;
        _currentUserService = currentUserService;
        _procedureService = procedureService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(PropertySearchFilterDto filter)
    {
        var properties = await _propertyService.SearchPropertiesAsync(filter);
        var customers = await _customerService.SearchCustomersAsync();

        ViewBag.Filter = filter;
        ViewBag.Customers = new SelectList(customers, "Id", "FullName", filter.CustomerId);

        return View(properties);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var property = await _propertyService.GetPropertyByIdAsync(id);
        if (property == null)
        {
            return NotFound("Property record not found.");
        }

        var allCustomers = await _customerService.SearchCustomersAsync();
        // Customers not yet associated
        var unassociatedCustomers = allCustomers
            .Where(c => c.Id != property.PrimaryCustomerId && property.AssociatedCustomers.All(ac => ac.CustomerId != c.Id))
            .ToList();

        ViewBag.AvailableCustomers = new SelectList(unassociatedCustomers, "Id", "FullName");

        // Load procedures for this property
        ViewBag.Procedures = await _procedureService.GetProceduresByPropertyAsync(id);

        return View(property);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId = null)
    {
        var customers = await _customerService.SearchCustomersAsync(status: CustomerStatus.Active);
        ViewBag.Customers = new SelectList(customers, "Id", "FullName", customerId);

        var dto = new CreatePropertyDto();
        if (customerId.HasValue)
        {
            dto.PrimaryCustomerId = customerId.Value;
        }

        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePropertyDto model)
    {
        if (!ModelState.IsValid)
        {
            var customers = await _customerService.SearchCustomersAsync(status: CustomerStatus.Active);
            ViewBag.Customers = new SelectList(customers, "Id", "FullName", model.PrimaryCustomerId);
            return View(model);
        }

        var result = await _propertyService.CreatePropertyAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = $"تم تسجيل العقار '{model.ReferenceNumber}' بنجاح.";
            return RedirectToAction(nameof(Details), new { id = result.Data });
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "Failed to create property record.");
        var customerList = await _customerService.SearchCustomersAsync(status: CustomerStatus.Active);
        ViewBag.Customers = new SelectList(customerList, "Id", "FullName", model.PrimaryCustomerId);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var property = await _propertyService.GetPropertyByIdAsync(id);
        if (property == null)
        {
            return NotFound("Property record not found.");
        }

        var customers = await _customerService.SearchCustomersAsync();
        ViewBag.Customers = new SelectList(customers, "Id", "FullName", property.PrimaryCustomerId);

        var updateDto = new UpdatePropertyDto
        {
            Id = property.Id,
            ReferenceNumber = property.ReferenceNumber,
            Type = property.Type,
            Title = property.Title,
            Description = property.Description,
            Governorate = property.Governorate,
            City = property.City,
            DistrictOrArea = property.DistrictOrArea,
            AddressDetails = property.AddressDetails,
            AreaMeasurement = property.AreaMeasurement,
            AreaUnit = property.AreaUnit,
            Latitude = property.Latitude,
            Longitude = property.Longitude,
            OwnershipType = property.OwnershipType,
            DeedOrTitleNumber = property.DeedOrTitleNumber,
            Status = property.Status,
            PrimaryCustomerId = property.PrimaryCustomerId,
            InternalNotes = property.InternalNotes
        };

        return View(updateDto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdatePropertyDto model)
    {
        if (!ModelState.IsValid)
        {
            var customers = await _customerService.SearchCustomersAsync();
            ViewBag.Customers = new SelectList(customers, "Id", "FullName", model.PrimaryCustomerId);
            return View(model);
        }

        var result = await _propertyService.UpdatePropertyAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = $"تم تحديث بيانات العقار '{model.ReferenceNumber}' بنجاح.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "Failed to update property record.");
        var customerList = await _customerService.SearchCustomersAsync();
        ViewBag.Customers = new SelectList(customerList, "Id", "FullName", model.PrimaryCustomerId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var result = await _propertyService.ArchivePropertyAsync(id, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تمت أرشفة سجل العقار بنجاح.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "فشلت عملية أرشفة العقار.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var result = await _propertyService.RestorePropertyAsync(id, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تمت استعادة سجل العقار وتنشيطه بنجاح.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "فشلت استعادة سجل العقار.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssociateCustomer(int propertyId, int customerId, string role, decimal? ownershipPercentage)
    {
        var result = await _propertyService.AssociateCustomerAsync(propertyId, customerId, role, ownershipPercentage, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم ربط العميل بالعقار بنجاح.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "فشلت عملية ربط العميل بالعقار.";
        }

        return RedirectToAction(nameof(Details), new { id = propertyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCustomerAssociation(int propertyId, int customerId)
    {
        var result = await _propertyService.RemoveCustomerAssociationAsync(propertyId, customerId, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم إلغاء ربط العميل بالعقار بنجاح.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "فشلت عملية إلغاء ربط العميل بالعقار.";
        }

        return RedirectToAction(nameof(Details), new { id = propertyId });
    }
}
