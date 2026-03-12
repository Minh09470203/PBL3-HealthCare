using Microsoft.AspNetCore.Mvc;

namespace PBL3_HealthCare.Controllers
{
    public class AccountController : Controller
    {
        public ActionResult Login()
        {
            return View();
        }

        public ActionResult Register()
        {
            return View();
        }
    }
}
