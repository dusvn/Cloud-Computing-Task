using Common.Interfaces;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace Client.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ClientController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Report()
        {
            var sales = await ServiceProxy.Create<IReportService>(new Uri("fabric:/CloudTask/ReportService")).GetSalesReportAsync();
            return Ok(sales);
        }

        [HttpGet]
        public async Task<IActionResult> Orders([FromQuery] Guid userId)
        {
            var orders = await ServiceProxy.Create<IValidate>(new Uri("fabric:/CloudTask/Validator")).GetOrders(userId);
            return Ok(orders);
        }

        [HttpPost]
        [EnableRateLimiting(global::Client.Client.PurchaseRateLimitPolicy)]
        public async Task<IActionResult> Purchase([FromBody]Purchase p)
        {
            PurchaseResponse res = await ServiceProxy.Create<IValidate>(new Uri("fabric:/CloudTask/Validator")).TransferData(p);
            return StatusCode(res.StatusCode, res);
        }
        
    }
}
