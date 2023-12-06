﻿using CodexContractsPlugin;
using Logging;
using Newtonsoft.Json;
using Utils;
using System.Numerics;

namespace CodexPlugin
{
    public interface IMarketplaceAccess
    {
        string MakeStorageAvailable(ByteSize size, TestToken minPriceForTotalSpace, TestToken maxCollateral, TimeSpan maxDuration);
        StoragePurchaseContract RequestStorage(ContentId contentId, TestToken pricePerSlotPerSecond, TestToken requiredCollateral, uint minRequiredNumberOfNodes, int proofProbability, TimeSpan duration);
        StoragePurchaseContract RequestStorage(ContentId contentId, TestToken pricePerSlotPerSecond, TestToken requiredCollateral, uint minRequiredNumberOfNodes, int proofProbability, TimeSpan duration, TimeSpan expiry);
    }

    public class MarketplaceAccess : IMarketplaceAccess
    {
        private readonly ILog log;
        private readonly CodexAccess codexAccess;

        public MarketplaceAccess(ILog log, CodexAccess codexAccess)
        {
            this.log = log;
            this.codexAccess = codexAccess;
        }

        public StoragePurchaseContract RequestStorage(ContentId contentId, TestToken pricePerSlotPerSecond, TestToken requiredCollateral, uint minRequiredNumberOfNodes, int proofProbability, TimeSpan duration)
        {
            return RequestStorage(contentId, pricePerSlotPerSecond, requiredCollateral, minRequiredNumberOfNodes, proofProbability, duration, duration / 2);
        }

        public StoragePurchaseContract RequestStorage(ContentId contentId, TestToken pricePerSlotPerSecond, TestToken requiredCollateral, uint minRequiredNumberOfNodes, int proofProbability, TimeSpan duration, TimeSpan expiry)
        {
            var expireUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiry.TotalSeconds;

            var request = new CodexSalesRequestStorageRequest
            {
                duration = ToDecInt(duration.TotalSeconds),
                proofProbability = ToDecInt(proofProbability),
                reward = ToDecInt(pricePerSlotPerSecond),
                collateral = ToDecInt(requiredCollateral),
                expiry = ToDecInt(expireUtc),
                nodes = minRequiredNumberOfNodes,
                tolerance = null,
            };

            Log($"Requesting storage for: {contentId.Id}... (" +
                $"pricePerSlotPerSecond: {pricePerSlotPerSecond}, " +
                $"requiredCollateral: {requiredCollateral}, " +
                $"minRequiredNumberOfNodes: {minRequiredNumberOfNodes}, " +
                $"proofProbability: {proofProbability}, " +
                $"expiry: {Time.FormatDuration(expiry)}, " +
                $"duration: {Time.FormatDuration(duration)})");

            var response = codexAccess.RequestStorage(request, contentId.Id);

            if (response == "Purchasing not available" || 
                response == "Expiry required" ||
                response == "Expiry needs to be in future")
            {
                throw new InvalidOperationException(response);
            }

            Log($"Storage requested successfully. PurchaseId: '{response}'.");

            return new StoragePurchaseContract(log, codexAccess, response, duration);
        }

        public string MakeStorageAvailable(ByteSize totalSpace, TestToken minPriceForTotalSpace, TestToken maxCollateral, TimeSpan maxDuration)
        {
            var request = new CodexSalesAvailabilityRequest
            {
                size = ToDecInt(totalSpace.SizeInBytes),
                duration = ToDecInt(maxDuration.TotalSeconds),
                maxCollateral = ToDecInt(maxCollateral),
                minPrice = ToDecInt(minPriceForTotalSpace)
            };

            Log($"Making storage available... (" +
                $"size: {totalSpace}, " +
                $"minPriceForTotalSpace: {minPriceForTotalSpace}, " +
                $"maxCollateral: {maxCollateral}, " +
                $"maxDuration: {Time.FormatDuration(maxDuration)})");

            var response = codexAccess.SalesAvailability(request);

            Log($"Storage successfully made available. Id: {response.id}");

            return response.id;
        }

        private string ToDecInt(double d)
        {
            var i = new BigInteger(d);
            return i.ToString("D");
        }

        public string ToDecInt(TestToken t)
        {
            var i = new BigInteger(t.Amount);
            return i.ToString("D");
        }

        private void Log(string msg)
        {
            log.Log($"{codexAccess.Container.Name} {msg}");
        }
    }

    public class MarketplaceUnavailable : IMarketplaceAccess
    {
        public StoragePurchaseContract RequestStorage(ContentId contentId, TestToken pricePerBytePerSecond, TestToken requiredCollateral, uint minRequiredNumberOfNodes, int proofProbability, TimeSpan duration)
        {
            Unavailable();
            return null!;
        }

        public StoragePurchaseContract RequestStorage(ContentId contentId, TestToken pricePerSlotPerSecond, TestToken requiredCollateral, uint minRequiredNumberOfNodes, int proofProbability, TimeSpan duration, TimeSpan expiry)
        {
            Unavailable();
            throw new NotImplementedException();
        }

        public string MakeStorageAvailable(ByteSize size, TestToken minPricePerBytePerSecond, TestToken maxCollateral, TimeSpan duration)
        {
            Unavailable();
            return string.Empty;
        }

        private void Unavailable()
        {
            FrameworkAssert.Fail("Incorrect test setup: Marketplace was not enabled for this group of Codex nodes. Add 'EnableMarketplace(...)' after 'SetupCodexNodes()' to enable it.");
            throw new InvalidOperationException();
        }
    }

    public class StoragePurchaseContract
    {
        private readonly ILog log;
        private readonly CodexAccess codexAccess;
        private DateTime? contractStartUtc;

        public StoragePurchaseContract(ILog log, CodexAccess codexAccess, string purchaseId, TimeSpan contractDuration)
        {
            this.log = log;
            this.codexAccess = codexAccess;
            PurchaseId = purchaseId;
            ContractDuration = contractDuration;
        }

        public string PurchaseId { get; }
        public TimeSpan ContractDuration { get; }

        public void WaitForStorageContractStarted()
        {
            WaitForStorageContractStarted(TimeSpan.FromSeconds(30));
        }

        public void WaitForStorageContractFinished()
        {
            if (!contractStartUtc.HasValue)
            {
                WaitForStorageContractStarted();
            }
            var gracePeriod = TimeSpan.FromSeconds(10);
            var currentContractTime = DateTime.UtcNow - contractStartUtc!.Value;
            var timeout = (ContractDuration - currentContractTime) + gracePeriod;
            WaitForStorageContractState(timeout, "finished");
        }

        public void WaitForStorageContractFinished(ByteSize contractFileSize)
        {
            if (!contractStartUtc.HasValue)
            {
                WaitForStorageContractStarted(contractFileSize.ToTimeSpan());
            }
            var gracePeriod = TimeSpan.FromSeconds(10);
            var currentContractTime = DateTime.UtcNow - contractStartUtc!.Value;
            var timeout = (ContractDuration - currentContractTime) + gracePeriod;
            WaitForStorageContractState(timeout, "finished");
        }

        /// <summary>
        /// Wait for contract to start. Max timeout depends on contract filesize. Allows more time for larger files.
        /// </summary>
        public void WaitForStorageContractStarted(ByteSize contractFileSize)
        {
            var filesizeInMb = contractFileSize.SizeInBytes / (1024 * 1024);
            var maxWaitTime = TimeSpan.FromSeconds(filesizeInMb * 10.0);

            WaitForStorageContractStarted(maxWaitTime);
        }

        public void WaitForStorageContractStarted(TimeSpan timeout)
        {
            WaitForStorageContractState(timeout, "started");
            contractStartUtc = DateTime.UtcNow;
        }

        private void WaitForStorageContractState(TimeSpan timeout, string desiredState)
        {
            var lastState = "";
            var waitStart = DateTime.UtcNow;

            log.Log($"Waiting for {Time.FormatDuration(timeout)} for contract '{PurchaseId}' to reach state '{desiredState}'.");
            while (lastState != desiredState)
            {
                var purchaseStatus = codexAccess.GetPurchaseStatus(PurchaseId);
                var statusJson = JsonConvert.SerializeObject(purchaseStatus);
                if (purchaseStatus != null && purchaseStatus.state != lastState)
                {
                    lastState = purchaseStatus.state;
                    log.Debug("Purchase status: " + statusJson);
                }

                Thread.Sleep(1000);

                if (lastState == "errored")
                {
                    FrameworkAssert.Fail("Contract errored: " + statusJson);
                }

                if (DateTime.UtcNow - waitStart > timeout)
                {
                    FrameworkAssert.Fail($"Contract did not reach '{desiredState}' within {Time.FormatDuration(timeout)} timeout. {statusJson}");
                }
            }
            log.Log($"Contract '{desiredState}'.");
        }

        public CodexStoragePurchase GetPurchaseStatus(string purchaseId)
        {
            return codexAccess.GetPurchaseStatus(purchaseId);
        }
    }
}
