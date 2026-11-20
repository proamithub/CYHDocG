using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Areas.Doctor.Controllers;

[Area("Doctor")]
[Authorize(Roles = "Doctor")]
public class PrescriptionsController : Controller
{
    private readonly IPrescriptionRepository _prescriptionRepo;

    public PrescriptionsController(IPrescriptionRepository prescriptionRepo)
    {
        _prescriptionRepo = prescriptionRepo;
    }

    [HttpGet]
    public IActionResult Create([FromQuery] long? userId, [FromQuery] long? consultationId)
    {
        ViewBag.TargetUserId = userId ?? 1;
        ViewBag.ConsultationId = consultationId;
        return View();
    }
}
