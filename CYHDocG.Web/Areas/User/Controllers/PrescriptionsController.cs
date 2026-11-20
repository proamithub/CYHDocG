using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.User.Controllers;

[Area("User")]
[Authorize(Roles = "User")]
public class PrescriptionsController : Controller
{
    private readonly IPrescriptionRepository _prescriptionRepo;

    public PrescriptionsController(IPrescriptionRepository prescriptionRepo)
    {
        _prescriptionRepo = prescriptionRepo;
    }

    public async Task<IActionResult> Index()
    {
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId);
        var prescriptions = await _prescriptionRepo.GetUserPrescriptionsAsync(userId);
        return View(prescriptions);
    }
}
