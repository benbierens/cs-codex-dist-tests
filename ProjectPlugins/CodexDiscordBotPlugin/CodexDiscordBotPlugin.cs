﻿using Core;
using KubernetesWorkflow;
using KubernetesWorkflow.Types;

namespace CodexDiscordBotPlugin
{
    public class CodexDiscordBotPlugin : IProjectPlugin, IHasLogPrefix, IHasMetadata
    {
        private readonly IPluginTools tools;

        public CodexDiscordBotPlugin(IPluginTools tools)
        {
            this.tools = tools;
        }

        public string LogPrefix => "(DiscordBot) ";

        public void Announce()
        {
            tools.GetLog().Log($"Codex DiscordBot (BiblioTech) loaded.");
        }

        public void AddMetadata(IAddMetadata metadata)
        {
            metadata.Add("codexdiscordbotid", new DiscordBotContainerRecipe().Image);
        }

        public void Decommission()
        {
        }

        public RunningPod Deploy(DiscordBotStartupConfig config)
        {
            var workflow = tools.CreateWorkflow();
            return StartContainer(workflow, config);
        }

        public RunningPod DeployRewarder(RewarderBotStartupConfig config)
        {
            var workflow = tools.CreateWorkflow();
            return StartRewarderContainer(workflow, config);
        }

        private RunningPod StartContainer(IStartupWorkflow workflow, DiscordBotStartupConfig config)
        {
            var startupConfig = new StartupConfig();
            startupConfig.NameOverride = config.Name;
            startupConfig.Add(config);
            return workflow.Start(1, new DiscordBotContainerRecipe(), startupConfig).WaitForOnline();
        }

        private RunningPod StartRewarderContainer(IStartupWorkflow workflow, RewarderBotStartupConfig config)
        {
            var startupConfig = new StartupConfig();
            startupConfig.Add(config);
            return workflow.Start(1, new RewarderBotContainerRecipe(), startupConfig).WaitForOnline();
        }
    }
}
