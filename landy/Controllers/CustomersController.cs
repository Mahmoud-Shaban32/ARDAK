using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Ardak.Model.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace landy.Controllers;

[Authorize(Policy = AppPolicies.RequireAdminOrSuperAdmin)]
public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        ICustomerService customerService,
        ICurrentUserService currentUserService,
        ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm = null, CustomerStatus? status = null)
    {
        var customers = await _customerService.SearchCustomersAsync(searchTerm, status);

        ViewBag.SearchTerm = searchTerm;
        ViewBag.Status = status;

        return View(customers);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null)
        {
            return NotFound("Customer record not found.");
        }

        return View(customer);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateCustomerDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateCustomerDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _customerService.CreateCustomerAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = $"تم إنشاء سجل العميل '{model.FullName}' بنجاح.";
            return RedirectToAction(nameof(Details), new { id = result.Data });
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "Failed to create customer record.");
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null)
        {
            return NotFound("Customer record not found.");
        }

        var updateDto = new UpdateCustomerDto
        {
            Id = customer.Id,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber,
            Email = customer.Email,
            Address = customer.Address,
            NationalIdOrTaxNumber = customer.NationalIdOrTaxNumber,
            Status = customer.Status,
            InternalNotes = customer.InternalNotes
        };

        return View(updateDto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateCustomerDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _customerService.UpdateCustomerAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = $"تم تحديث بيانات العميل '{model.FullName}' بنجاح.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        ModelState.AddModelError(string.Empty, result.Error ?? "Failed to update customer record.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var result = await _customerService.ArchiveCustomerAsync(id, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تمت أرشفة سجل العميل بنجاح.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "فشلت عملية أرشفة سجل العميل.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var result = await _customerService.RestoreCustomerAsync(id, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تمت استعادة سجل العميل وتنشيطه بنجاح.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "فشلت استعادة سجل العميل.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
