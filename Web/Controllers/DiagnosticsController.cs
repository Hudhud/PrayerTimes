using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Web.Controllers
{
    [ApiController]
    [Route("diag")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(IHttpClientFactory httpClientFactory, ILogger<DiagnosticsController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("muwaqqit")]
        public async Task<IActionResult> Muwaqqit([FromQuery] bool forceHttp11 = false)
        {
            const string url = "https://www.muwaqqit.com/api.json?lt=55.6759142&ln=12.5691285&d=2026-04-29&tz=Europe%2FCopenhagen&fa=-18.0&ea=-17.0&fea=0&rsa=0";
            var client = _httpClientFactory.CreateClient();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                if (forceHttp11)
                {
                    request.Version = HttpVersion.Version11;
                    request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
                }

                using var response = await client.SendAsync(request);
                var bodyPreview = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    success = response.IsSuccessStatusCode,
                    statusCode = (int)response.StatusCode,
                    responseVersion = response.Version.ToString(),
                    requestVersion = request.Version.ToString(),
                    forceHttp11,
                    headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                    bodyPreview = bodyPreview.Length > 500 ? bodyPreview.Substring(0, 500) : bodyPreview
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Muwaqqit diagnostic call failed. forceHttp11={ForceHttp11}", forceHttp11);

                return StatusCode(500, new
                {
                    success = false,
                    exceptionType = ex.GetType().FullName,
                    exceptionMessage = ex.Message,
                    innerExceptionType = ex.InnerException?.GetType().FullName,
                    innerExceptionMessage = ex.InnerException?.Message,
                    secondInnerExceptionType = ex.InnerException?.InnerException?.GetType().FullName,
                    secondInnerExceptionMessage = ex.InnerException?.InnerException?.Message,
                    forceHttp11
                });
            }
        }
    }
}
