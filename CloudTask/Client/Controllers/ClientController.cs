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
        [HttpPost]
        public async Task<IActionResult> Purchase([FromBody]Purchase p)
        {
            bool res = await ServiceProxy.Create<IValidate>(new Uri("fabric:/CloudTask/Validator")).TransferData(p);
            if (res) return Ok();
            else return BadRequest();
        }
        
    }
}
