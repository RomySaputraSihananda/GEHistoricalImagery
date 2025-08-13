using Google.Protobuf.WellKnownTypes;
using LibEsri;
using LibMapCommon;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GEHistoricalImagery.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class EsriWorldController : ControllerBase
    {
        [HttpGet("info")]
        public async Task<ActionResult<List<DatedEsriTile>>> Get(
            [FromQuery, BindRequired] double latitude,
            [FromQuery, BindRequired] double longitude,
            [FromQuery, BindRequired] int level = 10
        )
        {
            WayBack wayBack = await WayBack.CreateAsync("./cache");


            List<DatedEsriTile> list = new List<DatedEsriTile>();
            
            await foreach (
                var item in wayBack.GetDatesAsync(
                    EsriTile.GetTile(
                        new WebMercator(
                            x: latitude,
                            y: longitude
                        ),
                        level
                    )
                )
            ) list.Add(item);

            return Ok(
                list
            );
        }   

        [HttpGet("dump")]
        public Any Dumps(){
            return null;
        }
    }
}