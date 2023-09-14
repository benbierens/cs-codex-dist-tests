﻿using DistTestCore.Codex;
using DistTestCore.Logs;
using DistTestCore.Marketplace;
using DistTestCore.Metrics;
using Logging;
using NUnit.Framework;
using Utils;

namespace DistTestCore
{
    public interface IOnlineCodexNode
    {
        string GetName();
        CodexDebugResponse GetDebugInfo();
        CodexDebugPeerResponse GetDebugPeer(string peerId);
        ContentId UploadFile(TestFile file);
        TestFile? DownloadContent(ContentId contentId, string fileLabel = "");
        void ConnectToPeer(IOnlineCodexNode node);
        IDownloadedLog DownloadLog(int? tailLines = null);
        IDownloadedLog DownloadGethLog();
        IMetricsAccess Metrics { get; }
        IMarketplaceAccess Marketplace { get; }
        CodexDebugVersionResponse Version { get; }
        ICodexSetup BringOffline();
    }

    public class OnlineCodexNode : IOnlineCodexNode
    {
        private const string SuccessfullyConnectedMessage = "Successfully connected to peer";
        private const string UploadFailedMessage = "Unable to store block";
        private readonly TestLifecycle lifecycle;

        public OnlineCodexNode(TestLifecycle lifecycle, CodexAccess codexAccess, CodexNodeGroup group, IMetricsAccess metricsAccess, IMarketplaceAccess marketplaceAccess)
        {
            this.lifecycle = lifecycle;
            CodexAccess = codexAccess;
            Group = group;
            Metrics = metricsAccess;
            Marketplace = marketplaceAccess;
            Version = new CodexDebugVersionResponse();
        }

        public CodexAccess CodexAccess { get; }
        public CodexNodeGroup Group { get; }
        public IMetricsAccess Metrics { get; }
        public IMarketplaceAccess Marketplace { get; }
        public CodexDebugVersionResponse Version { get; private set; }

        public string GetName()
        {
            return CodexAccess.Container.Name;
        }

        public CodexDebugResponse GetDebugInfo()
        {
            var debugInfo = CodexAccess.GetDebugInfo();
            var known = string.Join(",", debugInfo.table.nodes.Select(n => n.peerId));
            Log($"Got DebugInfo with id: '{debugInfo.id}'. This node knows: {known}");
            return debugInfo;
        }

        public CodexDebugPeerResponse GetDebugPeer(string peerId)
        {
            return CodexAccess.GetDebugPeer(peerId);
        }

        public ContentId UploadFile(TestFile file)
        {
            using var fileStream = File.OpenRead(file.Filename);

            var logMessage = $"Uploading file {file.Describe()}...";
            Log(logMessage);
            var response = Stopwatch.Measure(lifecycle.Log, logMessage, () =>
            {
                return CodexAccess.UploadFile(fileStream);
            });

            if (string.IsNullOrEmpty(response)) Assert.Fail("Received empty response.");
            if (response.StartsWith(UploadFailedMessage)) Assert.Fail("Node failed to store block.");

            Log($"Uploaded file. Received contentId: '{response}'.");
            return new ContentId(response);
        }

        public TestFile? DownloadContent(ContentId contentId, string fileLabel = "")
        {
            var logMessage = $"Downloading for contentId: '{contentId.Id}'...";
            Log(logMessage);
            var file = lifecycle.FileManager.CreateEmptyTestFile(fileLabel);
            Stopwatch.Measure(lifecycle.Log, logMessage, () => DownloadToFile(contentId.Id, file));
            Log($"Downloaded file {file.Describe()} to '{file.Filename}'.");
            return file;
        }

        public void ConnectToPeer(IOnlineCodexNode node)
        {
            var peer = (OnlineCodexNode)node;

            Log($"Connecting to peer {peer.GetName()}...");
            var peerInfo = node.GetDebugInfo();
            var response = CodexAccess.ConnectToPeer(peerInfo.id, GetPeerMultiAddress(peer, peerInfo));

            Assert.That(response, Is.EqualTo(SuccessfullyConnectedMessage), "Unable to connect codex nodes.");
            Log($"Successfully connected to peer {peer.GetName()}.");
        }

        public IDownloadedLog DownloadLog(int? tailLines = null)
        {
            return lifecycle.DownloadLog(CodexAccess.Container, tailLines);
        }

        public IDownloadedLog DownloadGethLog()
        {
            return lifecycle.DownloadLog(Group.GethContainer);
        }

        public ICodexSetup BringOffline()
        {
            if (Group.Count() > 1) throw new InvalidOperationException("Codex-nodes that are part of a group cannot be " +
                "individually shut down. Use 'BringOffline()' on the group object to stop the group. This method is only " +
                "available for codex-nodes in groups of 1.");

            return Group.BringOffline();
        }

        public void EnsureOnlineGetVersionResponse()
        {
            var debugInfo = Time.Retry(CodexAccess.GetDebugInfo, "ensure online");
            var nodePeerId = debugInfo.id;
            var nodeName = CodexAccess.Container.Name;

            if (!debugInfo.codex.IsValid())
            {
                throw new Exception($"Invalid version information received from Codex node {GetName()}: {debugInfo.codex}");
            }

            lifecycle.Log.AddStringReplace(nodePeerId, nodeName);
            lifecycle.Log.AddStringReplace(debugInfo.table.localNode.nodeId, nodeName);
            Version = debugInfo.codex;
        }

        private string GetPeerMultiAddress(OnlineCodexNode peer, CodexDebugResponse peerInfo)
        {
            var multiAddress = peerInfo.addrs.First();
            // Todo: Is there a case where First address in list is not the way?

            // The peer we want to connect is in a different pod.
            // We must replace the default IP with the pod IP in the multiAddress.
            return multiAddress.Replace("0.0.0.0", peer.CodexAccess.Container.Pod.PodInfo.Ip);
        }

        private void DownloadToFile(string contentId, TestFile file)
        {
            using var fileStream = File.OpenWrite(file.Filename);
            try
            {
                using var downloadStream = CodexAccess.DownloadFile(contentId);
                downloadStream.CopyTo(fileStream);
            }
            catch
            {
                Log($"Failed to download file '{contentId}'.");
                throw;
            }
        }

        private void Log(string msg)
        {
            lifecycle.Log.Log($"{GetName()}: {msg}");
        }
    }

    public class ContentId
    {
        public ContentId(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}
