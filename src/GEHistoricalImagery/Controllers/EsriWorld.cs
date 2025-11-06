using GEHistoricalImagery.Cli;
using LibEsri;
using LibMapCommon;
using LibMapCommon.Geometry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Amazon.S3;
using Microsoft.AspNetCore.Builder;
using OSGeo.GDAL;
using System.IO;

namespace GEHistoricalImagery.Controllers
{
    /// <summary>
    /// Controller for handling Esri World tile data requests.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    public class EsriWorldController : ControllerBase
    {
        AmazonS3Client S3Client { get; set;}
        String BucketName { get; set; }

        String BasePathS3 { get; } = "data/data_gambar/esri-world";
        String AwsServiceURL { get; set; }

        public EsriWorldController()
        {
            var config = WebApplication.CreateBuilder().Configuration;
            this.BucketName = config["AwsSettings:S3BucketName"] ?? "";
            this.AwsServiceURL = config["AwsSettings:AwsServiceURL"] ?? "http://localhost:4566";
            this.S3Client = new AmazonS3Client(
                config["AwsSettings:AwsAccessKeyId"] ?? "",
                config["AwsSettings:AwsSecretAccessKey"] ?? "",
                new AmazonS3Config
                {
                    ServiceURL = this.AwsServiceURL,
                    ForcePathStyle = true
                }
            );
        }

        /// <summary>
        /// Gets dated tile information for the specified coordinates.
        /// </summary>
        /// <param name="latitude">
        /// Latitude in decimal degrees. North is positive, south is negative.
        /// </param>
        /// <param name="longitude">
        /// Longitude in decimal degrees. East is positive, west is negative.
        /// </param>
        /// <param name="level">
        /// Zoom level of the tiles (default: 10).
        /// </param>
        /// <returns>
        /// A list of dated tiles covering the specified location.
        /// </returns>
        [HttpGet("info")]
        [ProducesResponseType(typeof(IEnumerable<DatedEsriTile>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetInfo(
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
                        new Wgs1984(
                            latitude: latitude,
                            longitude: longitude
                        ).ToWebMercator(),
                        level
                    )
                )
            ) list.Add(item);

            return Ok(
                list
            );
        }


        private async Task<byte[]> DownloadTif<T>(FileInfo saveFile, DateOnly desiredDate, GeoRegion<T> region, TileStats stats, IEnumerable<Task<Download.TileDataset<T>>> generator, int level)
            where T : IGeoCoordinate<T>
        {
            var tempFile = Path.GetTempFileName();
            int numTilesDownload = 0;
            var processor = new ParallelProcessor<Download.TileDataset<T>>(1);

            using var fileLock = saveFile.Create();
            using var image = new EarthImage<T>(region, level, tempFile);
            using Dataset? missingTile = Download.CreateMissingTile();

            await foreach (var tds in processor.EnumerateResults(generator))
                using (tds)
                {
                    image.AddTile(tds.Tile, tds.Dataset ?? missingTile);
                    numTilesDownload++;
                    if (tds.Message is not null)
                        Console.Error.WriteLine($"\r\n{tds.Message}");
                }

            Console.WriteLine($"{numTilesDownload} out of {stats.TileCount} downloaded");

            //Release the lock for either saving or deleting
            fileLock.Dispose();

            if (numTilesDownload == 0)
            {
                if (saveFile.Exists)
                    saveFile.Delete();
                return null;
            }

            Console.Write("Saving Image: ");

            image.Save(saveFile.FullName, null, 1, 1d, 0d, 0d, false);

            byte[] bytes;
            using (FileStream fs = saveFile.OpenRead())
            using (MemoryStream ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                bytes = ms.ToArray();
            }

            saveFile.Delete();

            return bytes;
	}

        /// <summary>
        /// Retrieves Esri tiles for the specified geographic bounding box and date.
        /// </summary>
        /// <param name="max_latitude">
        /// The northernmost latitude in decimal degrees. North is positive, south is negative.
        /// </param>
        /// <param name="max_longitude">
        /// The easternmost longitude in decimal degrees. East is positive, west is negative.
        /// </param>
        /// <param name="min_latitude">
        /// The southernmost latitude in decimal degrees. North is positive, south is negative.
        /// </param>
        /// <param name="min_longitude">
        /// The westernmost longitude in decimal degrees. East is positive, west is negative.
        /// </param>
        /// <param name="date">
        /// The date for which the Esri tiles should be retrieved.
        /// </param>
        /// <param name="level">
        /// Zoom level of the tiles (default: 10).
        /// </param>
        /// <returns>
        /// A collection of Esri tiles that intersect the specified bounding box on the given date.
        /// </returns>
        [HttpGet("dump")]
        [ProducesResponseType(typeof(IEnumerable<EsriTile>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Dumps(
            [FromQuery, BindRequired] double max_latitude,
            [FromQuery, BindRequired] double max_longitude,
            [FromQuery, BindRequired] double min_latitude,
            [FromQuery, BindRequired] double min_longitude,
            [FromQuery, BindRequired] DateOnly date,
            [FromQuery, BindRequired] int level = 10
        ){
            WayBack wayBack = await WayBack.CreateAsync("./cache");

            GeoRegion<Wgs1984> Region = GeoRegion<Wgs1984>.Create(
                new Wgs1984(min_latitude, min_longitude),
                new Wgs1984(max_latitude, min_longitude),
                new Wgs1984(max_latitude, max_longitude),
                new Wgs1984(min_latitude, max_longitude)
            );
            var webMerc = Region.ToWebMercator();
            var layer = wayBack.Layers.OrderBy(l => int.Abs(l.Date.DayNumber - date.DayNumber)).First();
            var stats = webMerc.GetPolygonalRegionStats<EsriTile>(level);
			var aoi = webMerc.ToPixelRegion(level);

			var image = new EarthImage<Wgs1984>(Region, level, Path.GetTempFileName());
            using Dataset? missingTile = Download.CreateMissingTile();

            var processor = new ParallelProcessor<Download.TileDataset<Wgs1984>>(1);
			var saveFile = new FileInfo($"{Guid.NewGuid().ToString()}.tif");
			saveFile.Create().Dispose();

            byte[] tifData = await DownloadTif(
                saveFile,
                date,
                webMerc,
                stats,
                webMerc.GetTiles<EsriTile>(level).Select(
                    t => Task.Run(() => Download.DownloadTile(aoi, wayBack, t, date))
                ),
                level
            );

            Dump.FilenameFormatter formatter = new Dump.FilenameFormatter("{D}/{Z}/{c}-{r}", stats);

            return Ok(
                webMerc.GetTiles<EsriTile>(level).Select(
                    t => Task.Run(
                        async () =>
                        {
                            try
                            {
                                Dump.TileDataset data = await Dump.DownloadEsriTile(wayBack, t, date);

                                String format = $"{this.BasePathS3}/{min_latitude}_{min_longitude}_{max_latitude}_{max_longitude}/{formatter.GetString(data)}";

                                
                                string pathTif = $"{format.Substring(0, format.LastIndexOf('/'))}/image.tif";
                                
                                data.PathS3 = format + ".jpg";
                                var datasetCopy = data.Dataset;
                                _ = Task.Run(async () =>
                                {
                                    Console.WriteLine(pathTif);
                                    await this.S3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
                                    {
                                        BucketName = this.BucketName,
                                        Key = pathTif,
                                        InputStream = new MemoryStream(
                                            tifData
                                        ),
                                        ContentType = "image/tiff"
                                    });
                                    Console.WriteLine(format + ".json");
                                    await this.S3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
                                    {
                                        BucketName = this.BucketName,
                                        Key = format + ".json",
                                        InputStream = new MemoryStream(
                                            System.Text.Encoding.UTF8.GetBytes(
                                                System.Text.Json.JsonSerializer.Serialize(data)
                                            )
                                        ),
                                        ContentType = "application/json"
                                    });
                                    Console.WriteLine(format + ".jpg");
                                    await this.S3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
                                    {
                                        BucketName = this.BucketName,
                                        Key = format + ".jpg",
                                        InputStream = new MemoryStream(
                                            datasetCopy
                                        ),
                                        ContentType = "image/jpeg"
                                    });
                                });

                                data.UrlS3 = $"{this.AwsServiceURL}/{this.BucketName}/{data.PathS3}";
                                data.PathS3 = $"s3://{this.BucketName}/{data.PathS3}";
                                data.UrlS3Tif = $"{this.AwsServiceURL}/{this.BucketName}/{pathTif}";
                                data.PathS3Tif = $"s3://{this.BucketName}/{pathTif}";
                                data.Dataset = null;
                                return data;
                            }
                            catch (Exception err)
                            {
                                return null;
                            }
                        }
                    )
                )
            );
        }
    }
}