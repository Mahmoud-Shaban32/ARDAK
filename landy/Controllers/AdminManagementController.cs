using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace landy.Controllers;

[Authorize(Policy = AppPolicies.RequireSuperAdmin)]
public class AdminManagementController : Controller
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AdminManagementController> _logger;

    public AdminManagementController(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        ILogger<AdminManagementController> logger)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var admins = await _identityService.GetAdminsAsync();
        return View(admins);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateAdminDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAdminDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _identityService.CreateAdminAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = $"تم إنشاء حساب المشرف '{model.Email}' بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest();
        }

        var result = await _identityService.ToggleAdminStatusAsync(id, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم تحديث حالة حساب المشرف بنجاح.";
        }
        else
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors);
        }

        return RedirectToAction(nameof(Index));
    }
}
