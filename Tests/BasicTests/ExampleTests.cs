﻿using CodexContractsPlugin;
using CodexPlugin;
using DistTestCore;
using GethPlugin;
using MetricsPlugin;
using NUnit.Framework;
using Utils;

namespace Tests.BasicTests
{
    [TestFixture]
    public class ExampleTests : DistTest
    {
        [Test]
        public void CodexLogExample()
        {
            var primary = Ci.SetupCodexNode();

            primary.UploadFile(GenerateTestFile(5.MB()));

            var log = Ci.DownloadLog(primary);

            log.AssertLogContains("Uploaded file");
        }

        [Test]
        public void TwoMetricsExample()
        {
            var group = Ci.SetupCodexNodes(2, s => s.EnableMetrics());
            var group2 = Ci.SetupCodexNodes(2, s => s.EnableMetrics());

            var primary = group[0];
            var secondary = group[1];
            var primary2 = group2[0];
            var secondary2 = group2[1];

            var metrics = Ci.GetMetricsFor(primary, primary2);

            primary.ConnectToPeer(secondary);
            primary2.ConnectToPeer(secondary2);

            Thread.Sleep(TimeSpan.FromMinutes(2));

            metrics[0].AssertThat("libp2p_peers", Is.EqualTo(1));
            metrics[1].AssertThat("libp2p_peers", Is.EqualTo(1));
        }

        [Test]
        public void MarketplaceExample()
        {
            var geth = Ci.StartGethNode(s => s.IsMiner().WithName("disttest-geth"));

            var contracts = Ci.DeployCodexContracts(geth);

            var node = Ci.SetupCodexNode(s => s.EnableMarketplace(geth, contracts));

            var myBalance = geth.GetEthBalance();
            geth.SendEth(node, 10.Eth());
            var nodeBalance = geth.GetEthBalance(node);

            contracts.MintTestTokens(geth, node.EthAddress, 100.TestTokens());
            contracts.GetTestTokenBalance(geth, node.EthAddress);

            //var sellerInitialBalance = 234.TestTokens();
            //var buyerInitialBalance = 1000.TestTokens();
            //var fileSize = 10.MB();

            //var seller = Ci.SetupCodexNode(s => s
            //                .WithStorageQuota(11.GB())
            //                .EnableMarketplace(sellerInitialBalance));

            //seller.Marketplace.AssertThatBalance(Is.EqualTo(sellerInitialBalance));
            //seller.Marketplace.MakeStorageAvailable(
            //    size: 10.GB(),
            //    minPricePerBytePerSecond: 1.TestTokens(),
            //    maxCollateral: 20.TestTokens(),
            //    maxDuration: TimeSpan.FromMinutes(3));

            //var testFile = GenerateTestFile(fileSize);

            //var buyer = Ci.SetupCodexNode(s => s
            //    .WithBootstrapNode(seller)
            //    .EnableMarketplace(buyerInitialBalance));

            //buyer.Marketplace.AssertThatBalance(Is.EqualTo(buyerInitialBalance));
            
            //var contentId = buyer.UploadFile(testFile);
            //var purchaseContract = buyer.Marketplace.RequestStorage(contentId,
            //    pricePerSlotPerSecond: 2.TestTokens(),
            //    requiredCollateral: 10.TestTokens(),
            //    minRequiredNumberOfNodes: 1,
            //    proofProbability: 5,
            //    duration: TimeSpan.FromMinutes(1));

            //purchaseContract.WaitForStorageContractStarted(fileSize);

            //seller.Marketplace.AssertThatBalance(Is.LessThan(sellerInitialBalance), "Collateral was not placed.");

            //purchaseContract.WaitForStorageContractFinished();

            //seller.Marketplace.AssertThatBalance(Is.GreaterThan(sellerInitialBalance), "Seller was not paid for storage.");
            //buyer.Marketplace.AssertThatBalance(Is.LessThan(buyerInitialBalance), "Buyer was not charged for storage.");
        }
    }
}
