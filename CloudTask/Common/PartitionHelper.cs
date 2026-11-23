using System.Fabric;
using System.Security.Cryptography;
using System.Text;
using Microsoft.ServiceFabric.Services.Client;

namespace Common
{
    /// <summary>
    /// Deterministic routing of a business key (bookId / userId) to one of PartitionCount
    /// UniformInt64 partitions. Must match PartitionCount in ApplicationManifest.xml.
    /// </summary>
    public static class PartitionHelper
    {
        public const int PartitionCount = 4;

        public static int GetPartitionIndex(string key)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim().ToLowerInvariant()));
            return (int)(BitConverter.ToUInt32(hash, 0) % PartitionCount);
        }

        /// <summary>Low key of the partition range that owns <paramref name="key"/>.</summary>
        public static long GetPartitionLowKey(string key)
        {
            long step = (long)(((decimal)long.MaxValue - long.MinValue + 1) / PartitionCount);
            return long.MinValue + GetPartitionIndex(key) * step;
        }

        /// <summary>Index (0..PartitionCount-1) of the partition this replica belongs to.</summary>
        public static async Task<int> GetOwnPartitionIndexAsync(StatefulServiceContext context)
        {
            var partitions = await new FabricClient().QueryManager.GetPartitionListAsync(context.ServiceName);
            var own = partitions.First(x => x.PartitionInformation.Id == context.PartitionId);
            long low = ((Int64RangePartitionInformation)own.PartitionInformation).LowKey;
            long step = (long)(((decimal)long.MaxValue - long.MinValue + 1) / PartitionCount);
            return (int)((low - long.MinValue) / step);
        }

        public static ServicePartitionKey GetPartitionKey(string key) => new ServicePartitionKey(GetPartitionLowKey(key));
    }
}
