using Common.Interfaces;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
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

        [HttpPost]
        public async Task<IActionResult> Purchase([FromBody]Purchase p)
        {
            PurchaseResponse res = await ServiceProxy.Create<IValidate>(new Uri("fabric:/CloudTask/Validator")).TransferData(p);
            return StatusCode(res.StatusCode, res);
        }
        
    }
}
