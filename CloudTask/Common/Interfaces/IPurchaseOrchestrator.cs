using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces
{
    public interface IPurchaseOrchestrator : IService
    {
        /// <summary>
        /// If Purchase is done on correct way coordinator will return True 
        /// In other cases False
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        Task<bool> TransferDataAndPurchase(Purchase p);
    }
}
