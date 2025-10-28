using Microsoft.AspNetCore.Mvc;

namespace PersonalSchedulingAssistant.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult AboutUs()
        {
            return View();
        }

        public IActionResult WhatWeOffer()
        {
            return View();
        }
    }
}
