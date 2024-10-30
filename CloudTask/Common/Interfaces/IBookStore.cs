using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces
{
    public interface IBookStore : IService
    {
        public Task<double> GetItemPrice(string bookName);
    }
}
