﻿using DistTestCore;
using DistTestCore.Codex;
using Logging;

namespace ContinuousTests
{
    public interface ITestResultHandler
    {
        void TestPassed(ContinuousTest test);
        void TestFailed(ContinuousTest test);
    }

    public class ContinuousTestRunner : ITestResultHandler
    {
        private readonly ConfigLoader configLoader = new ConfigLoader();
        private readonly TestFactory testFactory = new TestFactory();
        private readonly CodexNodeFactory codexNodeFactory = new CodexNodeFactory();

        public void Run()
        {
            var config = configLoader.Load();
            StartupChecks(config);

            while (true)
            {
                var log = new FixtureLog(new LogConfig(config.LogPath, false), "ContinuousTestsRun");
                var allTestsRun = new AllTestsRun(config, log, testFactory, this);

                try
                {
                    allTestsRun.RunAll();
                }
                catch (Exception ex)
                {
                    log.Error($"Exception during test run: " + ex);
                }

                if (result == ContinuousTestResult.Failed)
                {
                    log.MarkAsFailed();
                }
                if (!config.KeepPassedTestLogs && result == ContinuousTestResult.Passed)
                {
                    log.DeleteFolder();
                }

                Thread.Sleep(config.SleepSecondsPerSingleTest * 1000);
            }
        }

        private void StartupChecks(Configuration config)
        {
            var log = new FixtureLog(new LogConfig(config.LogPath, false), "StartupChecks");
            log.Log("Starting continuous test run...");
            log.Log("Checking configuration...");
            PreflightCheck(config);
            log.Log("Contacting Codex nodes...");
            CheckCodexNodes(log, config);
            log.Log("All OK.");
        }

        private void PreflightCheck(Configuration config)
        {
            var tests = testFactory.CreateTests();
            if (!tests.Any())
            {
                throw new Exception("Unable to find any tests.");
            }

            var errors = new List<string>();
            foreach (var test in tests)
            {
                if (test.RequiredNumberOfNodes > config.CodexDeployment.CodexContainers.Length)
                {
                    errors.Add($"Test '{test.Name}' requires {test.RequiredNumberOfNodes} nodes. Deployment only has {config.CodexDeployment.CodexContainers.Length}");
                }
            }

            if (!Directory.Exists(config.LogPath))
            {
                Directory.CreateDirectory(config.LogPath);
            }

            if (errors.Any())
            {
                throw new Exception("Prerun check failed: " + string.Join(", ", errors));
            }
        }

        private void CheckCodexNodes(BaseLog log, Configuration config)
        {
            var nodes = codexNodeFactory.Create(config.CodexDeployment.CodexContainers, log, new DefaultTimeSet());
            var pass = true;
            foreach (var n in nodes)
            {
                log.Log($"Checking '{n.Address.Host}'...");

                if (EnsureOnline(n))
                {
                    log.Log("OK");
                }
                else
                {
                    log.Error($"No response from '{n.Address.Host}'.");
                    pass = false;
                }
            }
            if (!pass)
            {
                throw new Exception("Not all codex nodes responded.");
            }
        }

        private bool EnsureOnline(CodexNode n)
        {
            try
            {
                var info = n.GetDebugInfo();
                if (info == null || string.IsNullOrEmpty(info.id)) return false;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public void TestPassed(ContinuousTest test)
        {
            throw new NotImplementedException();
        }

        public void TestFailed(ContinuousTest test)
        {
            throw new NotImplementedException();
        }
    }
}
