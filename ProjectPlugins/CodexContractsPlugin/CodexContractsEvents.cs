﻿using CodexContractsPlugin.Marketplace;
using GethPlugin;
using Logging;
using Nethereum.Hex.HexTypes;
using NethereumWorkflow.BlockUtils;
using Utils;

namespace CodexContractsPlugin
{
    public interface ICodexContractsEvents
    {
        BlockInterval BlockInterval { get; }
        Request[] GetStorageRequests();
        RequestFulfilledEventDTO[] GetRequestFulfilledEvents();
        RequestCancelledEventDTO[] GetRequestCancelledEvents();
        RequestFailedEventDTO[] GetRequestFailedEvents();
        SlotFilledEventDTO[] GetSlotFilledEvents();
        SlotFreedEventDTO[] GetSlotFreedEvents();
    }

    public class CodexContractsEvents : ICodexContractsEvents
    {
        private readonly ILog log;
        private readonly IGethNode gethNode;
        private readonly CodexContractsDeployment deployment;

        public CodexContractsEvents(ILog log, IGethNode gethNode, CodexContractsDeployment deployment, BlockInterval blockInterval)
        {
            this.log = log;
            this.gethNode = gethNode;
            this.deployment = deployment;
            BlockInterval = blockInterval;
        }
        
        public BlockInterval BlockInterval { get; }

        public Request[] GetStorageRequests()
        {
            var events = gethNode.GetEvents<StorageRequestedEventDTO>(deployment.MarketplaceAddress, BlockInterval);
            var i = new ContractInteractions(log, gethNode);
            return events
            .Select(e =>
            {
                        var requestEvent = i.GetRequest(deployment.MarketplaceAddress, e.Event.RequestId);
                        var result = requestEvent.ReturnValue1;
                        result.Block = GetBlock(e.Log.BlockNumber.ToUlong());
                        result.RequestId = e.Event.RequestId;
                        return result;
                    })
                    .ToArray();
        }

        public RequestFulfilledEventDTO[] GetRequestFulfilledEvents()
        {
            var events = gethNode.GetEvents<RequestFulfilledEventDTO>(deployment.MarketplaceAddress, BlockInterval);
            return events.Select(e =>
            {
                var result = e.Event;
                result.Block = GetBlock(e.Log.BlockNumber.ToUlong());
                return result;
            }).ToArray();
        }

        public RequestCancelledEventDTO[] GetRequestCancelledEvents()
        {
            var events = gethNode.GetEvents<RequestCancelledEventDTO>(deployment.MarketplaceAddress, BlockInterval);
            return events.Select(e =>
            {
                var result = e.Event;
                result.Block = GetBlock(e.Log.BlockNumber.ToUlong());
                return result;
            }).ToArray();
        }

        public RequestFailedEventDTO[] GetRequestFailedEvents()
        {
            var events = gethNode.GetEvents<RequestFailedEventDTO>(deployment.MarketplaceAddress, BlockInterval);
            return events.Select(e =>
            {
                var result = e.Event;
                result.Block = GetBlock(e.Log.BlockNumber.ToUlong());
                return result;
            }).ToArray();
        }

        public SlotFilledEventDTO[] GetSlotFilledEvents()
        {
            var events = gethNode.GetEvents<SlotFilledEventDTO>(deployment.MarketplaceAddress, BlockInterval);
            return events.Select(e =>
            {
                var result = e.Event;
                result.Block = GetBlock(e.Log.BlockNumber.ToUlong());
                result.Host = GetEthAddressFromTransaction(e.Log.TransactionHash);
                return result;
            }).ToArray();
        }

        public SlotFreedEventDTO[] GetSlotFreedEvents()
        {
            var events = gethNode.GetEvents<SlotFreedEventDTO>(deployment.MarketplaceAddress, BlockInterval);
            return events.Select(e =>
            {
                var result = e.Event;
                result.Block = GetBlock(e.Log.BlockNumber.ToUlong());
                return result;
            }).ToArray();
        }

        private BlockTimeEntry GetBlock(ulong number)
        {
            return gethNode.GetBlockForNumber(number);
        }

        private EthAddress GetEthAddressFromTransaction(string transactionHash)
        {
            var transaction = gethNode.GetTransaction(transactionHash);
            return new EthAddress(transaction.From);
        }
    }
}
