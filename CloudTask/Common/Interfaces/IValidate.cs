using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
    public interface IValidate : IService
    {
        Task<PurchaseResponse> TransferData(Purchase p);
    }
}
