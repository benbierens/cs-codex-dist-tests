﻿using CodexDistTestCore.Config;
using CodexDistTestCore.Marketplace;
using k8s.Models;
using System.Collections;

namespace CodexDistTestCore
{
    public interface ICodexNodeGroup : IEnumerable<IOnlineCodexNode>
    {
        IOfflineCodexNodes BringOffline();
        IOnlineCodexNode this[int index] { get; }
    }

    public class CodexNodeGroup : ICodexNodeGroup
    {
        private readonly TestLog log;
        private readonly IK8sManager k8SManager;

        public CodexNodeGroup(TestLog log, int orderNumber, OfflineCodexNodes origin, IK8sManager k8SManager, OnlineCodexNode[] nodes)
        {
            this.log = log;
            OrderNumber = orderNumber;
            Origin = origin;
            this.k8SManager = k8SManager;
            Nodes = nodes;

            foreach (var n in nodes) n.Group = this;
        }

        public IOnlineCodexNode this[int index]
        {
            get
            {
                return Nodes[index];
            }
        }

        public IOfflineCodexNodes BringOffline()
        {
            return k8SManager.BringOffline(this);
        }

        public int OrderNumber { get; }
        public OfflineCodexNodes Origin { get; }
        public OnlineCodexNode[] Nodes { get; }
        public V1Deployment? Deployment { get; set; }
        public V1Service? Service { get; set; }
        public PodInfo? PodInfo { get; set; }
        public GethInfo? GethInfo { get; set; }

        public CodexNodeContainer[] GetContainers()
        {
            return Nodes.Select(n => n.Container).ToArray();
        }

        public IEnumerator<IOnlineCodexNode> GetEnumerator()
        {
            return Nodes.Cast<IOnlineCodexNode>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Nodes.GetEnumerator();
        }

        public V1ObjectMeta GetServiceMetadata()
        {
            return new V1ObjectMeta
            {
                Name = "codex-test-entrypoint-" + OrderNumber,
                NamespaceProperty = K8sCluster.K8sNamespace
            };
        }

        public V1ObjectMeta GetDeploymentMetadata()
        {
            return new V1ObjectMeta
            {
                Name = "codex-test-node-" + OrderNumber,
                NamespaceProperty = K8sCluster.K8sNamespace
            };
        }

        public CodexNodeLog DownloadLog(IOnlineCodexNode node)
        {
            var logDownloader = new PodLogDownloader(log, k8SManager);
            var n = (OnlineCodexNode)node;
            return logDownloader.DownloadLog(n);
        }

        public Dictionary<string, string> GetSelector()
        {
            return new Dictionary<string, string> { { "codex-test-node", "dist-test-" + OrderNumber } };
        }

        public string Describe()
        {
            return $"CodexNodeGroup#{OrderNumber}-{Origin.Describe()}";
        }
    }

    public class PodInfo
    {
        public PodInfo(string name, string ip)
        {
            Name = name;
            Ip = ip;
        }

        public string Name { get; }
        public string Ip { get; }
    }
}
