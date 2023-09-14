﻿using DistTestCore.Codex;
using DistTestCore.Marketplace;
using DistTestCore.Metrics;
using KubernetesWorkflow;
using Logging;

namespace DistTestCore
{
    public class CodexStarter : BaseStarter
    {
        public CodexStarter(TestLifecycle lifecycle)
            : base(lifecycle)
        {
        }

        public List<CodexNodeGroup> RunningGroups { get; } = new List<CodexNodeGroup>();

        public ICodexNodeGroup BringOnline(CodexSetup codexSetup)
        {
            LogSeparator();
            LogStart($"Starting {codexSetup.Describe()}...");
            var gethStartResult = lifecycle.GethStarter.BringOnlineMarketplaceFor(codexSetup);

            var startupConfig = CreateStartupConfig(gethStartResult, codexSetup);

            var containers = StartCodexContainers(startupConfig, codexSetup.NumberOfNodes, codexSetup.Location);

            var metricAccessFactory = CollectMetrics(codexSetup, containers);
            
            var codexNodeFactory = new CodexNodeFactory(lifecycle, metricAccessFactory, gethStartResult.MarketplaceAccessFactory);

            var group = CreateCodexGroup(codexSetup, containers, codexNodeFactory, gethStartResult.CompanionNode.RunningContainer);
            lifecycle.SetCodexVersion(group.Version);

            var nl = Environment.NewLine;
            var podInfos = string.Join(nl, containers.Containers().Select(c => $"Container: '{c.Name}' runs at '{c.Pod.PodInfo.K8SNodeName}'={c.Pod.PodInfo.Ip}"));
            LogEnd($"Started {codexSetup.NumberOfNodes} nodes " +
                $"of image '{containers.Containers().First().Recipe.Image}' " +
                $"and version '{group.Version}'{nl}" +
                podInfos);
            LogSeparator();

            return group;
        }

        public void BringOffline(CodexNodeGroup group)
        {
            LogStart($"Stopping {group.Describe()}...");
            var workflow = CreateWorkflow();
            foreach (var c in group.Containers)
            {
                StopCrashWatcher(c);
                workflow.Stop(c);
            }
            RunningGroups.Remove(group);
            LogEnd("Stopped.");
        }

        public void DeleteAllResources()
        {
            var workflow = CreateWorkflow();
            workflow.DeleteTestResources();

            RunningGroups.Clear();
        }

        public void DownloadLog(RunningContainer container, ILogHandler logHandler, int? tailLines)
        {
            var workflow = CreateWorkflow();
            workflow.DownloadContainerLog(container, logHandler, tailLines);
        }

        private IMetricsAccessFactory CollectMetrics(CodexSetup codexSetup, RunningContainers[] containers)
        {
            if (codexSetup.MetricsMode == MetricsMode.None) return new MetricsUnavailableAccessFactory();

            var runningContainers = lifecycle.PrometheusStarter.CollectMetricsFor(containers);

            if (codexSetup.MetricsMode == MetricsMode.Dashboard)
            {
                lifecycle.GrafanaStarter.StartDashboard(runningContainers.Containers.First(), codexSetup);
            }

            return new CodexNodeMetricsAccessFactory(lifecycle, runningContainers);
        }

        private StartupConfig CreateStartupConfig(GethStartResult gethStartResult, CodexSetup codexSetup)
        {
            var startupConfig = new StartupConfig();
            startupConfig.NameOverride = codexSetup.NameOverride;
            startupConfig.Add(codexSetup);
            startupConfig.Add(gethStartResult);
            return startupConfig;
        }

        private RunningContainers[] StartCodexContainers(StartupConfig startupConfig, int numberOfNodes, Location location)
        {
            var result = new List<RunningContainers>();
            var recipe = new CodexContainerRecipe();
            for (var i = 0; i < numberOfNodes; i++)
            {
                var workflow = CreateWorkflow();
                var rc = workflow.Start(1, location, recipe, startupConfig);
                CreateCrashWatcher(workflow, rc);
                result.Add(rc);
            }
            return result.ToArray();
        }

        private CodexNodeGroup CreateCodexGroup(CodexSetup codexSetup, RunningContainers[] runningContainers, CodexNodeFactory codexNodeFactory, RunningContainer gethContainer)
        {
            var group = new CodexNodeGroup(lifecycle, codexSetup, runningContainers, codexNodeFactory, gethContainer);
            RunningGroups.Add(group);

            try
            {
                Stopwatch.Measure(lifecycle.Log, "EnsureOnline", group.EnsureOnline, debug: true);
            }
            catch
            {
                CodexNodesNotOnline(runningContainers);
                throw;
            }

            return group;
        }

        private void CodexNodesNotOnline(RunningContainers[] runningContainers)
        {
            Log("Codex nodes failed to start");
            foreach (var container in runningContainers.Containers()) lifecycle.DownloadLog(container);
        }

        private StartupWorkflow CreateWorkflow()
        {
            return lifecycle.WorkflowCreator.CreateWorkflow();
        }

        private void LogSeparator()
        {
            Log("----------------------------------------------------------------------------");
        }

        private void CreateCrashWatcher(StartupWorkflow workflow, RunningContainers rc)
        {
            var c = rc.Containers.Single();
            c.CrashWatcher = workflow.CreateCrashWatcher(c);
        }

        private void StopCrashWatcher(RunningContainers containers)
        {
            foreach (var c in containers.Containers)
            {
                c.CrashWatcher?.Stop();
            }
        }
    }
}
