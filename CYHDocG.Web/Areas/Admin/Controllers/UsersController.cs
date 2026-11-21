using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly IAuthRepository _authRepo;

    public UsersController(IAuthRepository authRepo)
    {
        _authRepo = authRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? q)
    {
        var users = await _authRepo.GetAllUsersForAdminAsync(q);
        ViewBag.SearchTerm = q ?? "";
        return View(users.ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(long userId)
    {
        var result = await _authRepo.ToggleUserActiveStatusAsync(userId);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
