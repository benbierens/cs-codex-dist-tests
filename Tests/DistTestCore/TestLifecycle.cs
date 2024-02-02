﻿using Core;
using DistTestCore.Logs;
using FileUtils;
using KubernetesWorkflow;
using KubernetesWorkflow.Recipe;
using KubernetesWorkflow.Types;
using Utils;

namespace DistTestCore
{
    public class TestLifecycle : IK8sHooks
    {
        private const string TestsType = "dist-tests";
        private readonly EntryPoint entryPoint;
        private readonly Dictionary<string, string> metadata; 
        private readonly List<RunningContainers> runningContainers = new();
        private readonly string deployId;

        public TestLifecycle(TestLog log, Configuration configuration, ITimeSet timeSet, string testNamespace, string deployId)
        {
            Log = log;
            Configuration = configuration;
            TimeSet = timeSet;
            TestStart = DateTime.UtcNow;

            entryPoint = new EntryPoint(log, configuration.GetK8sConfiguration(timeSet, this, testNamespace), configuration.GetFileManagerFolder(), timeSet);
            metadata = entryPoint.GetPluginMetadata();
            CoreInterface = entryPoint.CreateInterface();
            this.deployId = deployId;

            log.WriteLogTag();
        }

        public DateTime TestStart { get; }
        public TestLog Log { get; }
        public Configuration Configuration { get; }
        public ITimeSet TimeSet { get; }
        public CoreInterface CoreInterface { get; }

        public void DeleteAllResources()
        {
            entryPoint.Decommission(
                deleteKubernetesResources: true,
                deleteTrackedFiles: true
            );
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

        public TimeSpan GetTestDuration()
        {
            return DateTime.UtcNow - TestStart;
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
            recipe.PodLabels.Add("runid", deployId);
            recipe.PodLabels.Add("testid", NameUtils.GetTestId());
            recipe.PodLabels.Add("category", NameUtils.GetCategoryName());
            recipe.PodLabels.Add("fixturename", NameUtils.GetRawFixtureName());
            recipe.PodLabels.Add("testname", NameUtils.GetTestMethodName());
            recipe.PodLabels.Add("testframeworkrevision", GitInfo.GetStatus());

            foreach (var pair in metadata)
            {
                recipe.PodLabels.Add(pair.Key, pair.Value);
            }
        }

        public void DownloadAllLogs()
        {
            foreach (var rc in runningContainers)
            {
                foreach (var c in rc.Containers)
                {
                    CoreInterface.DownloadLog(c);
                }
            }
        }
    }
}
