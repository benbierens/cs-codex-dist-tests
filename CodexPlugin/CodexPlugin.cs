﻿using Core;
using KubernetesWorkflow;
using Logging;

namespace CodexPlugin
{
    public class CodexPlugin : IProjectPlugin
    {
        private CodexStarter codexStarter = null!;

        #region IProjectPlugin Implementation

        public void Announce(ILog log)
        {
            log.Log("hello from codex plugin. codex container info here.");
        }

        public void Initialize(IPluginTools tools)
        {
            codexStarter = new CodexStarter(tools);
        }

        public void Finalize(ILog log)
        {
        }

        #endregion

        public RunningContainers[] StartCodexNodes(int numberOfNodes, Action<ICodexSetup> setup)
        {
            var codexSetup = new CodexSetup(numberOfNodes, CodexLogLevel.Trace);
            setup(codexSetup);
            return codexStarter.BringOnline(codexSetup);
        }

        public ICodexNodeGroup WrapCodexContainers(RunningContainers[] containers)
        {
            return codexStarter.WrapCodexContainers(containers);
        }

        public IOnlineCodexNode SetupCodexNode(Action<ICodexSetup> setup)
        {
            return SetupCodexNodes(1, setup)[0];
        }

        public ICodexNodeGroup SetupCodexNodes(int number, Action<ICodexSetup> setup)
        {
            var rc = StartCodexNodes(number, setup);
            return WrapCodexContainers(rc);
        }

        public ICodexNodeGroup SetupCodexNodes(int number)
        {
            var rc = StartCodexNodes(number, s => { });
            return WrapCodexContainers(rc);
        }
    }
}
