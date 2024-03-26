﻿using CodexContractsPlugin;
using Newtonsoft.Json.Linq;
using System.Numerics;

namespace CodexPlugin
{
    public class Mapper
    {
        public DebugInfo Map(CodexOpenApi.DebugInfo debugInfo)
        {
            return new DebugInfo
            {
                Id = debugInfo.Id,
                Spr = debugInfo.Spr,
                Addrs = debugInfo.Addrs.ToArray(),
                AnnounceAddresses = JArray(debugInfo.AdditionalProperties, "announceAddresses").Select(x => x.ToString()).ToArray(),
                Version = MapDebugInfoVersion(JObject(debugInfo.AdditionalProperties, "codex")),
                Table = MapDebugInfoTable(JObject(debugInfo.AdditionalProperties, "table"))
            };
        }

        public LocalDataset[] Map(ICollection<CodexOpenApi.DataList> dataList)
        {
            return Array.Empty<LocalDataset>();
        }

        public CodexOpenApi.SalesAvailabilityCREATE Map(StorageAvailability availability)
        {
            return new CodexOpenApi.SalesAvailabilityCREATE
            {
                Duration = ToDecInt(availability.MaxDuration.TotalSeconds),
                MinPrice = ToDecInt(availability.MinPriceForTotalSpace),
                MaxCollateral = ToDecInt(availability.MaxCollateral),
                TotalSize = ToDecInt(availability.TotalSpace.SizeInBytes)
            };
        }

        public CodexOpenApi.StorageRequestCreation Map(StoragePurchaseRequest purchase)
        {
            return new CodexOpenApi.StorageRequestCreation
            {
                Duration = ToDecInt(purchase.Duration.TotalSeconds),
                ProofProbability = ToDecInt(purchase.ProofProbability),
                Reward = ToDecInt(purchase.PricePerSlotPerSecond),
                Collateral = ToDecInt(purchase.RequiredCollateral),
                Expiry = ToDecInt(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + purchase.Expiry.TotalSeconds),
                Nodes = purchase.MinRequiredNumberOfNodes,
                Tolerance = purchase.NodeFailureTolerance
            };
        }

        public StoragePurchase Map(CodexOpenApi.Purchase purchase)
        {
            return new StoragePurchase
            {
                State = purchase.State,
                Error = purchase.Error
            };
        }

        private DebugInfoVersion MapDebugInfoVersion(JObject obj)
        {
            return new DebugInfoVersion
            {
                Version = StringOrEmpty(obj, "version"),
                Revision = StringOrEmpty(obj, "revision")
            };
        }

        private DebugInfoTable MapDebugInfoTable(JObject obj)
        {
            return new DebugInfoTable
            {
                LocalNode = MapDebugInfoTableNode(obj.GetValue("localNode")),
                Nodes = new DebugInfoTableNode[0]
            };
        }

        private DebugInfoTableNode MapDebugInfoTableNode(JToken? token)
        {
            var obj = token as JObject;
            if (obj == null) return new DebugInfoTableNode();

            return new DebugInfoTableNode
            {
                Address = StringOrEmpty(obj, "address"),
                NodeId = StringOrEmpty(obj, "nodeId"),
                PeerId = StringOrEmpty(obj, "peerId"),
                Record = StringOrEmpty(obj, "record"),
                Seen = Bool(obj, "seen")
            };
        }

        private JArray JArray(IDictionary<string, object> map, string name)
        {
            return (JArray)map[name];
        }

        private JObject JObject(IDictionary<string, object> map, string name)
        {
            return (JObject)map[name];
        }

        private string StringOrEmpty(JObject obj, string name)
        {
            if (obj.TryGetValue(name, out var token))
            {
                var str = (string?)token;
                if (!string.IsNullOrEmpty(str)) return str;
            }
            return string.Empty;
        }

        private bool Bool(JObject obj, string name)
        {
            if (obj.TryGetValue(name, out var token))
            {
                return (bool)token;
            }
            return false;
        }

        private string ToDecInt(double d)
        {
            var i = new BigInteger(d);
            return i.ToString("D");
        }

        private string ToDecInt(TestToken t)
        {
            var i = new BigInteger(t.Amount);
            return i.ToString("D");
        }
    }
}
