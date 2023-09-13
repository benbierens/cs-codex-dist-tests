﻿using Core;
using FileUtils;
using KubernetesWorkflow;
using Logging;
using Utils;

namespace DistTestCore
{
    public class TestLifecycle : IK8sHooks
    {
        private const string TestsType = "dist-tests";
        private readonly DateTime testStart;
        private readonly EntryPoint entryPoint;
        private readonly List<RunningContainers> runningContainers = new List<RunningContainers>();

        public TestLifecycle(TestLog log, Configuration configuration, ITimeSet timeSet, string testNamespace)
        {
            Log = log;
            Configuration = configuration;
            TimeSet = timeSet;
            testStart = DateTime.UtcNow;

            entryPoint = new EntryPoint(log, configuration.GetK8sConfiguration(timeSet, this, testNamespace), configuration.GetFileManagerFolder(), timeSet);
            CoreInterface = entryPoint.CreateInterface();

            log.WriteLogTag();
        }

        public TestLog Log { get; }
        public Configuration Configuration { get; }
        public ITimeSet TimeSet { get; }
        public CoreInterface CoreInterface { get; }

        public void DeleteAllResources()
        {
            entryPoint.Tools.CreateWorkflow().DeleteNamespace();
            entryPoint.Tools.GetFileManager().DeleteAllFiles();
            entryPoint.Decommission();
        }

        public TrackedFile GenerateTestFile(ByteSize size, string label = "")
        {
            return entryPoint.Tools.GetFileManager().GenerateFile(size, label);
        }

        public IFileManager GetFileManager()
        {
            return entryPoint.Tools.GetFileManager();
        }

        public Dictionary<string, string> GetPluginMetadata()
        {
            return entryPoint.GetPluginMetadata();
        }

        public string GetTestDuration()
        {
            var testDuration = DateTime.UtcNow - testStart;
            return Time.FormatDuration(testDuration);
        }

        public void OnContainersStarted(RunningContainers rc)
        {
            runningContainers.Add(rc);
        }

        public void OnContainersStopped(RunningContainers rc)
        {
            runningContainers.Remove(rc);
        }

        public void OnContainerRecipeCreated(ContainerRecipe recipe)
        {
            recipe.PodLabels.Add("tests-type", TestsType);
            recipe.PodLabels.Add("runid", NameUtils.GetRunId());
            recipe.PodLabels.Add("testid", NameUtils.GetTestId());
            recipe.PodLabels.Add("category", NameUtils.GetCategoryName());
            recipe.PodLabels.Add("fixturename", NameUtils.GetRawFixtureName());
            recipe.PodLabels.Add("testname", NameUtils.GetTestMethodName());
        }

        public void DownloadAllLogs()
        {
            var workflow = entryPoint.Tools.CreateWorkflow();
            foreach (var rc in runningContainers)
            {
                foreach (var c in rc.Containers)
                {
                    DownloadContainerLog(workflow, c);
                }
            }
        }

        private void DownloadContainerLog(IStartupWorkflow workflow, RunningContainer c)
        {
            var file = Log.CreateSubfile();
            Log.Log($"Downloading container log for '{c.Name}' to file '{file.FullFilename}'...");
            var handler = new LogDownloadHandler(c.Name, file);
            workflow.DownloadContainerLog(c, handler);
        }
    }
}
