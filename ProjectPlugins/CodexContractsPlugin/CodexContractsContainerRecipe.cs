﻿using GethPlugin;
using KubernetesWorkflow;

namespace CodexContractsPlugin
{
    public class CodexContractsContainerRecipe : ContainerRecipeFactory
    {
        public static string DockerImage { get; } = "codexstorage/codex-contracts-eth:sha-1854dfb-dist-tests";

        public const string MarketplaceAddressFilename = "/hardhat/deployments/codexdisttestnetwork/Marketplace.json";
        public const string MarketplaceArtifactFilename = "/hardhat/artifacts/contracts/Marketplace.sol/Marketplace.json";

        public override string AppName => "codex-contracts";
        public override string Image => DockerImage;

        protected override void Initialize(StartupConfig startupConfig)
        {
            var config = startupConfig.Get<CodexContractsContainerConfig>();

            var containerPort = config.GethNode.StartResult.Container.GetContainerPort(GethContainerRecipe.HttpPortTag);

            var ip = config.GethNode.StartResult.Container.Pod.PodInfo.Ip;
            var port = containerPort.InternalAddress.Port;

            AddEnvVar("DISTTEST_NETWORK_URL", $"http://{ip}:{port}");
            AddEnvVar("HARDHAT_NETWORK", "codexdisttestnetwork");
            AddEnvVar("KEEP_ALIVE", "1");
        }
    }
}
