using Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace Client.Controllers
{
    /// <summary>Read endpoints. Purchases themselves are in <see cref="PurchasesController"/>.</summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ClientController : ControllerBase
    {
        private static IValidate Validator => ServiceProxy.Create<IValidate>(new Uri("fabric:/CloudTask/Validator"));

        [HttpGet]
        public async Task<IActionResult> Report()
        {
            var sales = await ServiceProxy.Create<IReportService>(new Uri("fabric:/CloudTask/ReportService")).GetSalesReportAsync();
            return Ok(sales);
        }

        /// <summary>Orders of one user (fan-out over all orchestrator partitions).</summary>
        [HttpGet]
        public async Task<IActionResult> Orders([FromQuery] Guid userId) => Ok(await Validator.GetOrders(userId));

        /// <summary>Books that can be bought, with available stock (fan-out over all Bookstore partitions).</summary>
        [HttpGet]
        public async Task<IActionResult> Books() => Ok(await Validator.ListAvailableItems());

        /// <summary>Users with their balance (fan-out over all Bank partitions).</summary>
        [HttpGet]
        public async Task<IActionResult> Clients() => Ok(await Validator.ListClients());
    }
}
