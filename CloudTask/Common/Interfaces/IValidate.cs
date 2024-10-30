using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Common.Interfaces
{
    public interface IValidate : IService
    {
        Task<bool> TransferData(Purchase p);
    }
}
