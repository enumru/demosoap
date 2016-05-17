using System.Web.Mvc;

namespace Enum2.DemoSoap.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [Authorize]
        public ActionResult Secure()
        {
            return View();
        }
    }
}