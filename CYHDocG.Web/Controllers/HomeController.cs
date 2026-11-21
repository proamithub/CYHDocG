using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CYHDocG.Web.Models;
using CYHDocG.Repo.Interfaces;

namespace CYHDocG.Web.Controllers;

public class HomeController : Controller
{
    private readonly IFaqRepository _faqRepo;

    public HomeController(IFaqRepository faqRepo)
    {
        _faqRepo = faqRepo;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var faqs = await _faqRepo.GetActiveFaqsAsync();
            ViewBag.Faqs = faqs.ToList();
        }
        catch
        {
            ViewBag.Faqs = new List<CYHDocG.Repo.DTOs.FaqDto>();
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public async Task<IActionResult> Faq()
    {
        try
        {
            var faqs = await _faqRepo.GetActiveFaqsAsync();
            return View(faqs.ToList());
        }
        catch
        {
            return View(new List<CYHDocG.Repo.DTOs.FaqDto>());
        }
    }

    public IActionResult Emergency()
    {
        return View();
    }

    public IActionResult Terms()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
