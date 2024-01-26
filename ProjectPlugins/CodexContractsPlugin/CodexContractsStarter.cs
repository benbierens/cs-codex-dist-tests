﻿using Core;
using GethPlugin;
using KubernetesWorkflow;
using KubernetesWorkflow.Types;
using Logging;
using Utils;

namespace CodexContractsPlugin
{
    public class CodexContractsStarter
    {
        private readonly IPluginTools tools;

        public CodexContractsStarter(IPluginTools tools)
        {
            this.tools = tools;
        }

        public CodexContractsDeployment Deploy(CoreInterface ci, IGethNode gethNode)
        {
            Log("Starting Codex SmartContracts container...");

            var workflow = tools.CreateWorkflow();
            var startupConfig = CreateStartupConfig(gethNode);
            startupConfig.NameOverride = "codex-contracts";

            var containers = workflow.Start(1, new CodexContractsContainerRecipe(), startupConfig);
            if (containers.Containers.Length != 1) throw new InvalidOperationException("Expected 1 Codex contracts container to be created. Test infra failure.");
            var container = containers.Containers[0];

            Log("Container started.");

            try
            {
                var result = DeployContract(container, workflow, gethNode);
                workflow.Stop(containers);
                Log("Container stopped.");
                return result;
            }
            catch (Exception ex)
            {
                Log("Failed to deploy contract: " + ex);
                Log("Downloading Codex SmartContracts container log...");
                ci.DownloadLog(container);
                throw;
            }
        }

        public ICodexContracts Wrap(IGethNode gethNode, CodexContractsDeployment deployment)
        {
            return new CodexContractsAccess(tools.GetLog(), gethNode, deployment);
        }

        private CodexContractsDeployment DeployContract(RunningContainer container, IStartupWorkflow workflow, IGethNode gethNode)
        {
            Log("Deploying SmartContract...");
            WaitUntil(() =>
            {
                var logHandler = new ContractsReadyLogHandler(tools.GetLog());
                workflow.DownloadContainerLog(container, logHandler, 100);
                return logHandler.Found;
            });
            Log("Contracts deployed. Extracting addresses...");

            var extractor = new ContractsContainerInfoExtractor(tools.GetLog(), workflow, container);
            var marketplaceAddress = extractor.ExtractMarketplaceAddress();
            var abi = extractor.ExtractMarketplaceAbi();

            var interaction = new ContractInteractions(tools.GetLog(), gethNode);
            var tokenAddress = interaction.GetTokenAddress(marketplaceAddress);

            Log("Extract completed. Checking sync...");

            Time.WaitUntil(() => interaction.IsSynced(marketplaceAddress, abi));

            Log("Synced. Codex SmartContracts deployed.");

            return new CodexContractsDeployment(marketplaceAddress, abi, tokenAddress);
        }

        private void Log(string msg)
        {
            tools.GetLog().Log(msg);
        }

        private void WaitUntil(Func<bool> predicate)
        {
            Time.WaitUntil(predicate, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(2));
        }

        private StartupConfig CreateStartupConfig(IGethNode gethNode)
        {
            var startupConfig = new StartupConfig();
            var contractsConfig = new CodexContractsContainerConfig(gethNode);
            startupConfig.Add(contractsConfig);
            return startupConfig;
        }
    }

    public class ContractsReadyLogHandler : LogHandler
    {
        // Log should contain 'Compiled 15 Solidity files successfully' at some point.
        private const string RequiredCompiledString = "Solidity files successfully";
        // When script is done, it prints the ready-string.
        private const string ReadyString = "Done! Sleeping indefinitely...";
        private readonly ILog log;

        public ContractsReadyLogHandler(ILog log)
        {
            this.log = log;

            log.Debug($"Looking for '{RequiredCompiledString}' and '{ReadyString}' in container logs...");
        }

        public bool SeenCompileString { get; private set; }
        public bool Found { get; private set; }

        protected override void ProcessLine(string line)
        {
            log.Debug(line);
            if (line.Contains(RequiredCompiledString)) SeenCompileString = true;
            if (line.Contains(ReadyString))
            {
                if (!SeenCompileString) throw new Exception("CodexContracts deployment failed. " +
                    "Solidity files not compiled before process exited.");

                Found = true;
            }
        }
    }
}
