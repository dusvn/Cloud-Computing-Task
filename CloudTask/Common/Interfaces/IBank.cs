using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces
{
    public interface IBank : IService
    {
        public Task<bool> CheckAmountOfMoney(Purchase p);
    }
}
