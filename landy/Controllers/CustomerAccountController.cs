using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Model.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace landy.Controllers;

[Authorize(Policy = AppPolicies.RequireAdminOrSuperAdmin)]
public class CustomerAccountController : Controller
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAppDbContext _dbContext;

    public CustomerAccountController(
        IIdentityService identityService,
        ICurrentUserService currentUserService,
        IAppDbContext dbContext)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Provision(int customerId)
    {
        var customer = await _dbContext.Customers.FindAsync(customerId);
        if (customer == null)
        {
            return NotFound("Customer record not found.");
        }

        if (!string.IsNullOrEmpty(customer.IdentityUserId))
        {
            TempData["ErrorMessage"] = "هذا العميل لديه حساب تسجيل دخول مرتبط بالفعل.";
            return RedirectToAction("Index", "InternalDashboard");
        }

        var dto = new CreateCustomerAccountDto
        {
            CustomerId = customer.Id,
            Email = customer.Email ?? string.Empty
        };

        ViewBag.CustomerName = customer.FullName;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Provision(CreateCustomerAccountDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _identityService.CreateCustomerAccountAsync(model, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = $"تم تفعيل حساب البوابة بنجاح للعميل '{model.Email}'.";
            return RedirectToAction("Index", "InternalDashboard");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int customerId)
    {
        var result = await _identityService.ToggleCustomerAccountStatusAsync(customerId, _currentUserService.UserId!);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "تم تحديث حالة حساب العميل بنجاح.";
        }
        else
        {
            TempData["ErrorMessage"] = string.Join("; ", result.Errors);
        }

        return RedirectToAction("Index", "InternalDashboard");
    }
}
