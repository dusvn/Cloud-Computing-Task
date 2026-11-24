using Common.Interfaces;
using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace Client.Controllers
{
    /// <summary>
    /// Purchases as a resource:
    ///   POST   /api/purchases        header Idempotency-Key → 202 + Location (saga runs in the background)
    ///   GET    /api/purchases/{id}   current saga state
    ///   DELETE /api/purchases/{id}   202 before the pivot (compensation), 409 after
    /// The purchase id IS the idempotency key.
    /// </summary>
    [ApiController]
    [Route("api/purchases")]
    public class PurchasesController : ControllerBase
    {
        private const string IdempotencyKeyHeader = "Idempotency-Key";

        private static IValidate Validator => ServiceProxy.Create<IValidate>(new Uri("fabric:/CloudTask/Validator"));

        [HttpPost]
        [EnableRateLimiting(global::Client.Client.PurchaseRateLimitPolicy)]
        public async Task<IActionResult> Create([FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey, [FromBody] Purchase purchase)
        {
            if (!Guid.TryParse(idempotencyKey, out Guid key) || key == Guid.Empty)
                return BadRequest(PurchaseResponse.BadRequest($"Header {IdempotencyKeyHeader} is required and must be a UUID."));

            purchase.IdempotencyKey = key;
            PurchaseResponse response = await Validator.TransferData(purchase);

            if (response.IsAccepted)
                return AcceptedAtAction(nameof(Get), new { id = key }, response);   // 202 + Location: /api/purchases/{id}
            return StatusCode(response.StatusCode, response);                     // 400 from validation
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            OrderRecord? purchase = await Validator.GetPurchase(id);
            return purchase is null ? NotFound(PurchaseResponse.NotFound()) : Ok(purchase);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            PurchaseResponse response = await Validator.CancelPurchase(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
