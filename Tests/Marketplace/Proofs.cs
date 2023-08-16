﻿using DistTestCore;
using DistTestCore.Codex;
using NUnit.Framework;
using Utils;

namespace Tests.BasicTests
{
    [TestFixture]
    public class MarketplaceTests : DistTest
    {
        [Test]
        public void HostThatMissesProofsIsPaidOutLessThanHostThatDoesNotMissProofs()
        {
            var sellerInitialBalance = 234.TestTokens();
            var buyerInitialBalance = 1000.TestTokens();

            var seller = SetupCodexNode(s => s
                .WithLogLevel(CodexLogLevel.Trace, "marketplace", "sales", "proving", "reservations")
                .WithStorageQuota(11.GB())
                .EnableMarketplace(sellerInitialBalance)
                .WithName("seller"));

            var sellerWithFailures = SetupCodexNode(s => s
                .WithLogLevel(CodexLogLevel.Trace, "marketplace", "sales", "proving", "reservations")
                .WithStorageQuota(11.GB())
                .WithBootstrapNode(seller)
                .WithSimulateProofFailures(2)
                .EnableMarketplace(sellerInitialBalance)
                .WithName("seller with failures"));

            var buyer = SetupCodexNode(s => s
                .WithLogLevel(CodexLogLevel.Trace, "marketplace", "purchasing", "node", "restapi")
                .WithBootstrapNode(seller)
                .EnableMarketplace(buyerInitialBalance)
                .WithName("buyer"));

            var validator = SetupCodexNode(s => s
                .WithLogLevel(CodexLogLevel.Trace, "validator")
                .WithBootstrapNode(seller)
                // .WithValidator()
                .EnableMarketplace(0.TestTokens(), 2.Eth(), true)
                .WithName("validator"));

            seller.Marketplace.AssertThatBalance(Is.EqualTo(sellerInitialBalance));
            sellerWithFailures.Marketplace.AssertThatBalance(Is.EqualTo(sellerInitialBalance));
            buyer.Marketplace.AssertThatBalance(Is.EqualTo(buyerInitialBalance));

            seller.Marketplace.MakeStorageAvailable(
                size: 10.GB(),
                minPricePerBytePerSecond: 1.TestTokens(),
                maxCollateral: 20.TestTokens(),
                maxDuration: TimeSpan.FromMinutes(5));

            sellerWithFailures.Marketplace.MakeStorageAvailable(
                size: 10.GB(),
                minPricePerBytePerSecond: 1.TestTokens(),
                maxCollateral: 20.TestTokens(),
                maxDuration: TimeSpan.FromMinutes(5));

            var testFile = GenerateTestFile(10.MB());
            var contentId = buyer.UploadFile(testFile);

            buyer.Marketplace.RequestStorage(contentId,
                pricePerSlotPerSecond: 2.TestTokens(),
                requiredCollateral: 10.TestTokens(),
                minRequiredNumberOfNodes: 2,
                proofProbability: 2,
                duration: TimeSpan.FromMinutes(4));

            // Time.Sleep(TimeSpan.FromMinutes(1));

            // seller.Marketplace.AssertThatBalance(Is.LessThan(sellerInitialBalance), "Collateral was not placed.");

            Time.Sleep(TimeSpan.FromMinutes(3));

            var sellerBalance = seller.Marketplace.GetBalance();
            sellerWithFailures.Marketplace.AssertThatBalance(Is.LessThan(sellerBalance), "Seller that was slashed should have less balance than seller that was not slashed.");
        }
    }
}
