﻿using Core;
using KubernetesWorkflow;
using Logging;

namespace MetricsPlugin
{
    public static class CoreInterfaceExtensions
    {
        public static RunningContainers StartMetricsCollector(this CoreInterface ci, params IMetricsScrapeTarget[] scrapeTargets)
        {
            return Plugin(ci).StartMetricsCollector(scrapeTargets);
        }

        public static IMetricsAccess GetMetricsFor(this CoreInterface ci, RunningContainers metricsContainer, IMetricsScrapeTarget scrapeTarget)
        {
            return Plugin(ci).CreateAccessForTarget(metricsContainer, scrapeTarget);
        }

        public static IMetricsAccess[] GetMetricsFor(this CoreInterface ci, params IMetricsScrapeTarget[] scrapeTargets)
        {
            var rc = ci.StartMetricsCollector(scrapeTargets);
            return scrapeTargets.Select(t => ci.GetMetricsFor(rc, t)).ToArray();
        }

        public static LogFile? DownloadAllMetrics(this CoreInterface ci, IMetricsAccess metricsAccess, string targetName)
        {
            return Plugin(ci).DownloadAllMetrics(metricsAccess, targetName);
        }

        private static MetricsPlugin Plugin(CoreInterface ci)
        {
            return ci.GetPlugin<MetricsPlugin>();
        }
    }
}
