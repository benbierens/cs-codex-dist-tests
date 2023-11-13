﻿using Core;
using GethPlugin;
using KubernetesWorkflow;
using KubernetesWorkflow.Types;

namespace CodexPlugin
{
    public interface ICodexNodeFactory
    {
        CodexNode CreateOnlineCodexNode(CodexAccess access, CodexNodeGroup group);
        CrashWatcher CreateCrashWatcher(RunningContainer c);
    }

    public class CodexNodeFactory : ICodexNodeFactory
    {
        private readonly IPluginTools tools;

        public CodexNodeFactory(IPluginTools tools)
        {
            this.tools = tools;
        }

        public CodexNode CreateOnlineCodexNode(CodexAccess access, CodexNodeGroup group)
        {
            var ethAddress = GetEthAddress(access);
            var marketplaceAccess = GetMarketplaceAccess(access, ethAddress);
            return new CodexNode(tools, access, group, marketplaceAccess, ethAddress);
        }

        private IMarketplaceAccess GetMarketplaceAccess(CodexAccess codexAccess, EthAddress? ethAddress)
        {
            if (ethAddress == null) return new MarketplaceUnavailable();
            return new MarketplaceAccess(tools.GetLog(), codexAccess);
        }

        private EthAddress? GetEthAddress(CodexAccess access)
        {
            var mStart = access.Container.Recipe.Additionals.Get<MarketplaceStartResults>();
            if (mStart == null) return null;
            return mStart.EthAddress;
        }

        public CrashWatcher CreateCrashWatcher(RunningContainer c)
        {
            return tools.CreateWorkflow().CreateCrashWatcher(c);
        }
    }
}
