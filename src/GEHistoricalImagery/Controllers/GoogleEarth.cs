using GEHistoricalImagery.Cli;
using LibGoogleEarth;
using LibMapCommon;
using LibMapCommon.Geometry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GEHistoricalImagery.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class GoogleEarthController : ControllerBase
    {
        [HttpGet("info")]
        public async Task<ActionResult<IEnumerable<DatedTile>>> GetInfo(
            [FromQuery, BindRequired] double latitude,
            [FromQuery, BindRequired] double longitude,
            [FromQuery, BindRequired] int level = 10
        )
        {
            DbRoot root = await DbRoot.CreateAsync(Database.TimeMachine, "./cache");
            
            TileNode? node = await root.GetNodeAsync(
                KeyholeTile.GetTile(
                    new Wgs1984(
                        latitude: latitude,
                        longitude: longitude
                    ),
                    level
                )
            );

            return node != null ? Ok(node.GetAllDatedTiles()) : NotFound(node);
        }

        [HttpGet("dump")]
        public async Task<object> Dumps(
            [FromQuery, BindRequired] double max_latitude,
            [FromQuery, BindRequired] double max_longitude,
            [FromQuery, BindRequired] double min_latitude,
            [FromQuery, BindRequired] double min_longitude,
            [FromQuery, BindRequired] DateOnly date,
            [FromQuery, BindRequired] int level = 10
        )
        {
            DbRoot root = await DbRoot.CreateAsync(Database.TimeMachine, "./cache");

            if (max_longitude < min_longitude)
                max_longitude += 360;

            GeoRegion<Wgs1984> Region = GeoRegion<Wgs1984>.Create(
                new Wgs1984(min_latitude, min_longitude),
                new Wgs1984(max_latitude, min_longitude),
                new Wgs1984(max_latitude, max_longitude),
                new Wgs1984(min_latitude, max_longitude)
            );
            TileStats stats = Region.GetPolygonalRegionStats<KeyholeTile>(level);

            Dump.FilenameFormatter formatter = new Dump.FilenameFormatter("z={Z}-Col={c}-Row={r}.jpg", stats);

            return Region
                .GetTiles<KeyholeTile>(level)
                .Select(t => Task.Run(async () => {
                        var data = await Dump.DownloadTile(root, t, date);
                        Console.WriteLine(data);
                        return data;
                    })
                );
        }
    }
}