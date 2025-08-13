using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace GEHistoricalImagery.Controllers
{ 
    [ApiController]
    [Route("")]
    public class RootController : ControllerBase
    {
        private readonly IConfiguration _config;
        
        public RootController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public object Get()
        {
            var version = _config["ApiSettings:Version"] ?? "1.0.0";
            var title = _config["ApiSettings:Title"] ?? "Hello World API";
            
            return new
            {
                Title = title,
                Version = version,
                Message = "Hello World! This is a public endpoint.",
                Info = "Use X-Api-Key header for protected endpoints",
                SwaggerUrl = "/swagger"
            };
        }
    }
}