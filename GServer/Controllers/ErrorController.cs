using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GServer.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger<HomeController> _logger;
     
        //injected objects to controller
        public ErrorController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        //GET: /controller/
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    ViewBag.ErrorMessage = "Sorry, the page could not be found.";
                    break;
            }
            
            return View("ErrorNotFound");
        }

    }
}
