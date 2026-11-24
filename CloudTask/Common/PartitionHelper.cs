using System.Fabric;
using System.Security.Cryptography;
using System.Text;
using Microsoft.ServiceFabric.Services.Client;
using Murmur;

namespace Common
{
    /// <summary>
    /// Routes a business key (UserId / BookId) to a UniformInt64 Service Fabric partition.
    /// The key is hashed with MurmurHash3 to a signed 64-bit number; that number is used directly as the
    /// partition key, so Service Fabric picks the partition whose [LowKey, HighKey] range contains it.
    /// Works for any PartitionCount configured in ApplicationManifest.xml.
    /// </summary>
    public static class PartitionHelper
    {
        // MurmurHash3 x64 128-bit from the "murmurhash" NuGet package; the HashAlgorithm is not thread-safe, so one per call.
        /// <summary>Signed 64-bit MurmurHash3 (first 8 bytes of the 128-bit hash) of the key (trimmed, lower-case).</summary>
        public static long GetHashKey(string key)
        {
            using HashAlgorithm murmur = MurmurHash.Create128(seed: 0, managed: true, preference: AlgorithmPreference.X64);
            byte[] hash = murmur.ComputeHash(Encoding.UTF8.GetBytes(key.Trim().ToLowerInvariant()));
            return BitConverter.ToInt64(hash, 0);
        }

        // Guid ids: the canonical lower-case text form is hashed, so every service computes the same value.
        public static long GetHashKey(Guid id) => GetHashKey(id.ToString());

        public static ServicePartitionKey GetPartitionKey(Guid id) => new(GetHashKey(id));

        /// <summary>
        /// Orchestrator partition of a purchase. The purchase id IS the idempotency key, so a repeated request
        /// always reaches the partition that already holds the order, whichever user or client sends it.
        /// </summary>
        public static ServicePartitionKey ForPurchase(Guid purchaseId) => GetPartitionKey(purchaseId);

        /// <summary>One key per partition of the service (its LowKey), for fan-out calls to every partition.</summary>
        public static async Task<List<ServicePartitionKey>> GetAllPartitionKeysAsync(Uri serviceName)
        {
            var partitions = await new FabricClient().QueryManager.GetPartitionListAsync(serviceName);
            return partitions
                .Select(p => new ServicePartitionKey(((Int64RangePartitionInformation)p.PartitionInformation).LowKey))
                .ToList();
        }

        /// <summary>The [LowKey, HighKey] key range of the partition this replica belongs to.</summary>
        public static async Task<(long LowKey, long HighKey)> GetOwnKeyRangeAsync(StatefulServiceContext context)
        {
            var partitions = await new FabricClient().QueryManager.GetPartitionListAsync(context.ServiceName);
            var own = partitions.First(p => p.PartitionInformation.Id == context.PartitionId);
            var range = (Int64RangePartitionInformation)own.PartitionInformation;
            return (range.LowKey, range.HighKey);
        }

        /// <summary>True when <paramref name="id"/> hashes into <paramref name="range"/> (i.e. the partition owns it).</summary>
        public static bool IsInRange(Guid id, (long LowKey, long HighKey) range)
        {
            long hash = GetHashKey(id);
            return hash >= range.LowKey && hash <= range.HighKey;
        }
    }
}
