using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces
{
    public interface ITransaction : IService
    {
        Task Commit(Purchase purchase);
        /// <summary>
        /// This method check is all ok for distributed transaction in context of availability of resources 
        /// Then lock this resource 
        /// </summary>
        /// <param name="purchase"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<bool> PrepareAndLock(Purchase purchase,Guid token);

        Task RollBack(Purchase p);   

    }
}
