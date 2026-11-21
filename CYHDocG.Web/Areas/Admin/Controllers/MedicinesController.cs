using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.DTOs;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class MedicinesController : Controller
{
    private readonly IPrescriptionRepository _prescriptionRepo;

    public MedicinesController(IPrescriptionRepository prescriptionRepo)
    {
        _prescriptionRepo = prescriptionRepo;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? q)
    {
        IEnumerable<MasterMedicineDto> list;
        if (!string.IsNullOrWhiteSpace(q))
        {
            list = await _prescriptionRepo.SearchMasterMedicinesAsync(q);
        }
        else
        {
            list = await _prescriptionRepo.GetAllMasterMedicinesAsync(100);
        }
        ViewBag.SearchTerm = q ?? "";
        return View(list.ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(MasterMedicineDto model)
    {
        if (string.IsNullOrWhiteSpace(model.BrandName))
        {
            TempData["ErrorMessage"] = "Brand name is required";
            return RedirectToAction(nameof(Index));
        }

        var result = await _prescriptionRepo.AddMasterMedicineAsync(model);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
