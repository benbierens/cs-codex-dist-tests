﻿using KubernetesWorkflow;

namespace DistTestCore.Codex
{
    public class CodexAccess
    {
        private readonly TestLifecycle lifecycle;

        public CodexAccess(TestLifecycle lifecycle, RunningContainer runningContainer)
        {
            this.lifecycle = lifecycle;
            Container = runningContainer;
        }

        public RunningContainer Container { get; }

        public CodexDebugResponse GetDebugInfo()
        {
            return Http(TimeSpan.FromSeconds(2)).HttpGetJson<CodexDebugResponse>("debug/info");
        }

        public CodexDebugPeerResponse GetDebugPeer(string peerId)
        {
            return GetDebugPeer(peerId, TimeSpan.FromSeconds(2));
        }

        public CodexDebugPeerResponse GetDebugPeer(string peerId, TimeSpan timeout)
        {
            var http = Http(timeout);
            var str = http.HttpGetString($"debug/peer/{peerId}");

            if (str.ToLowerInvariant() == "unable to find peer!")
            {
                return new CodexDebugPeerResponse
                {
                    IsPeerFound = false
                };
            }

            var result = http.TryJsonDeserialize<CodexDebugPeerResponse>(str);
            result.IsPeerFound = true;
            return result;
        }

        public string UploadFile(FileStream fileStream)
        {
            return Http().HttpPostStream("upload", fileStream);
        }

        public Stream DownloadFile(string contentId)
        {
            return Http().HttpGetStream("download/" + contentId);
        }

        public CodexSalesAvailabilityResponse SalesAvailability(CodexSalesAvailabilityRequest request)
        {
            return Http().HttpPostJson<CodexSalesAvailabilityRequest, CodexSalesAvailabilityResponse>("sales/availability", request);
        }

        public string RequestStorage(CodexSalesRequestStorageRequest request, string contentId)
        {
            return Http().HttpPostJson($"storage/request/{contentId}", request);
        }

        public string ConnectToPeer(string peerId, string peerMultiAddress)
        {
            return Http().HttpGetString($"connect/{peerId}?addrs={peerMultiAddress}");
        }

        public void EnsureOnline()
        {
            try
            {
                var debugInfo = GetDebugInfo();
                if (debugInfo == null || string.IsNullOrEmpty(debugInfo.id)) throw new InvalidOperationException("Unable to get debug-info from codex node at startup.");

                var nodePeerId = debugInfo.id;
                var nodeName = Container.Name;
                lifecycle.Log.AddStringReplace(nodePeerId, nodeName);
                lifecycle.Log.AddStringReplace(debugInfo.table.localNode.nodeId, nodeName);
            }
            catch (Exception e)
            {
                lifecycle.Log.Error($"Failed to start codex node: {e}. Test infra failure.");
                throw new InvalidOperationException($"Failed to start codex node. Test infra failure.", e);
            }
        }

        private Http Http(TimeSpan? timeoutOverride = null)
        {
            var address = lifecycle.Configuration.GetAddress(Container);
            return new Http(lifecycle.Log, lifecycle.TimeSet, address, baseUrl: "/api/codex/v1", timeoutOverride);
        }
    }

    public class CodexDebugResponse
    {
        public string id { get; set; } = string.Empty;
        public string[] addrs { get; set; } = new string[0];
        public string repo { get; set; } = string.Empty;
        public string spr { get; set; } = string.Empty;
        public EnginePeerResponse[] enginePeers { get; set; } = Array.Empty<EnginePeerResponse>();
        public SwitchPeerResponse[] switchPeers { get; set; } = Array.Empty<SwitchPeerResponse>();
        public CodexDebugVersionResponse codex { get; set; } = new();
        public CodexDebugTableResponse table { get; set; } = new();
    }

    public class CodexDebugTableResponse
    {
        public CodexDebugTableNodeResponse localNode { get; set; } = new();
        public CodexDebugTableNodeResponse[] nodes { get; set; } = Array.Empty<CodexDebugTableNodeResponse>();
    }

    public class CodexDebugTableNodeResponse
    {
        public string nodeId { get; set; } = string.Empty;
        public string peerId { get; set; } = string.Empty;
        public string record { get; set; } = string.Empty;
        public string address { get; set; } = string.Empty;
        public bool seen { get; set; }
    }

    public class EnginePeerResponse
    {
        public string peerId { get; set; } = string.Empty;
        public EnginePeerContextResponse context { get; set; } = new();
    }

    public class EnginePeerContextResponse
    {
        public int blocks { get; set; } = 0;
        public int peerWants { get; set; } = 0;
        public int exchanged { get; set; } = 0;
        public string lastExchange { get; set; } = string.Empty;
    }

    public class SwitchPeerResponse
    {
        public string peerId { get; set; } = string.Empty;
        public string key { get; set; } = string.Empty;
    }

    public class CodexDebugVersionResponse
    {
        public string version { get; set; } = string.Empty;
        public string revision { get; set; } = string.Empty;
    }

    public class CodexDebugPeerResponse
    {
        public bool IsPeerFound { get; set; }

        public string peerId { get; set; } = string.Empty;
        public long seqNo { get; set; }
        public CodexDebugPeerAddressResponse[] addresses { get; set; } = Array.Empty<CodexDebugPeerAddressResponse>();
    }

    public class CodexDebugPeerAddressResponse
    {
        public string address { get; set; } = string.Empty;
    }

    public class CodexSalesAvailabilityRequest
    {
        public string size { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;
        public string minPrice { get; set; } = string.Empty;
        public string maxCollateral { get; set; } = string.Empty;
    }

    public class CodexSalesAvailabilityResponse
    {
        public string id { get; set; } = string.Empty;
        public string size { get; set; } = string.Empty;
        public string duration { get; set; } = string.Empty;
        public string minPrice { get; set; } = string.Empty;
        public string maxCollateral { get; set; } = string.Empty;
    }

    public class CodexSalesRequestStorageRequest
    {
        public string duration { get; set; } = string.Empty;
        public string proofProbability { get; set; } = string.Empty;
        public string reward { get; set; } = string.Empty;
        public string collateral { get; set; } = string.Empty;
        public string? expiry { get; set; }
        public uint? nodes { get; set; }
        public uint? tolerance { get; set;}
    }
}
