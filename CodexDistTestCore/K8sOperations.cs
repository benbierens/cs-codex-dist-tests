﻿using k8s;
using k8s.Models;
using NUnit.Framework;

namespace CodexDistTestCore
{
    public class K8sOperations
    {
        public const string K8sNamespace = "codex-test-namespace";

        private readonly CodexDockerImage dockerImage = new CodexDockerImage();
        private readonly Kubernetes client;
        private readonly KnownK8sPods knownPods;

        public K8sOperations(KnownK8sPods knownPods)
        {
            this.knownPods = knownPods;

            // todo: If the default KubeConfig file does not suffice, change it here:
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            client = new Kubernetes(config);
        }

        public void BringOnline(CodexNodeGroup online, OfflineCodexNodes offline)
        {
            EnsureTestNamespace();

            CreateDeployment(online, offline);
            CreateService(online);

            WaitUntilOnline(online);
            AssignActivePodNames(online);
        }

        public void BringOffline(CodexNodeGroup online)
        {
            var deploymentName = online.Deployment.Name();
            DeleteDeployment(online);
            DeleteService(online);
            WaitUntilOffline(deploymentName);
        }

        public void DeleteAllResources()
        {
            DeleteNamespace();

            WaitUntilZeroPods();
            WaitUntilNamespaceDeleted();
        }

        public void FetchAllPodsLogs(CodexNodeGroup[] onlines, IPodLogsHandler logHandler)
        {
            foreach (var online in onlines)
            {
                var nodeDescription = online.Describe();
                foreach (var podName in online.ActivePodNames)
                {
                    var stream = client.ReadNamespacedPodLog(podName, K8sNamespace);
                    logHandler.Log(online.OrderNumber, $"{nodeDescription}:{podName}", stream);
                }
            }
        }

        private void AssignActivePodNames(CodexNodeGroup online)
        {
            var pods = client.ListNamespacedPod(K8sNamespace);
            var podNames = pods.Items.Select(p => p.Name());
            foreach (var podName in podNames)
            {
                if (!knownPods.Contains(podName))
                {
                    knownPods.Add(podName);
                    online.ActivePodNames.Add(podName);
                }
            }
        }

        #region Waiting

        private void WaitUntilOnline(CodexNodeGroup online)
        {
            WaitUntil(() =>
            {
                online.Deployment = client.ReadNamespacedDeployment(online.Deployment.Name(), K8sNamespace);
                return online.Deployment?.Status.AvailableReplicas != null && online.Deployment.Status.AvailableReplicas > 0;
            });
        }

        private void WaitUntilOffline(string deploymentName)
        {
            WaitUntil(() =>
            {
                var deployment = client.ReadNamespacedDeployment(deploymentName, K8sNamespace);
                return deployment == null || deployment.Status.AvailableReplicas == 0;
            });
        }

        private void WaitUntilZeroPods()
        {
            WaitUntil(() => !client.ListNamespacedPod(K8sNamespace).Items.Any());
        }

        private void WaitUntilNamespaceDeleted()
        {
            WaitUntil(() => !IsTestNamespaceOnline());
        }

        private void WaitUntil(Func<bool> predicate)
        {
            var start = DateTime.UtcNow;
            var state = predicate();
            while (!state)
            {
                if (DateTime.UtcNow - start > Timing.K8sOperationTimeout())
                {
                    Assert.Fail("K8s operation timed out.");
                    throw new TimeoutException();
                }

                Timing.WaitForK8sServiceDelay();
                state = predicate();
            }
        }

        #endregion

        #region Service management

        private void CreateService(CodexNodeGroup online)
        {
            var serviceSpec = new V1Service
            {
                ApiVersion = "v1",
                Metadata = online.GetServiceMetadata(),
                Spec = new V1ServiceSpec
                {
                    Type = "NodePort",
                    Selector = online.GetSelector(),
                    Ports = CreateServicePorts(online)
                }
            };

            online.Service = client.CreateNamespacedService(serviceSpec, K8sNamespace);
        }

        private List<V1ServicePort> CreateServicePorts(CodexNodeGroup online)
        {
            var result = new List<V1ServicePort>();
            var containers = online.GetContainers();
            foreach (var container in containers)
            {
                result.Add(new V1ServicePort
                {
                    Name = container.ServicePortName,
                    Protocol = "TCP",
                    Port = container.ApiPort,
                    TargetPort = container.ContainerPortName,
                    NodePort = container.ServicePort
                });
            }
            return result;
        }

        private void DeleteService(CodexNodeGroup online)
        {
            if (online.Service == null) return;
            client.DeleteNamespacedService(online.Service.Name(), K8sNamespace);
            online.Service = null;
        }

        #endregion

        #region Deployment management

        private void CreateDeployment(CodexNodeGroup online, OfflineCodexNodes offline)
        {
            var deploymentSpec = new V1Deployment
            {
                ApiVersion = "apps/v1",
                Metadata = online.GetDeploymentMetadata(),
                Spec = new V1DeploymentSpec
                {
                    Replicas = 1,
                    Selector = new V1LabelSelector
                    {
                        MatchLabels = online.GetSelector()
                    },
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Labels = online.GetSelector()
                        },
                        Spec = new V1PodSpec
                        {
                            Containers = CreateDeploymentContainers(online, offline)
                        }
                    }
                }
            };

            online.Deployment = client.CreateNamespacedDeployment(deploymentSpec, K8sNamespace);
        }

        private List<V1Container> CreateDeploymentContainers(CodexNodeGroup online, OfflineCodexNodes offline)
        {
            var result = new List<V1Container>();
            var containers = online.GetContainers();
            foreach (var container in containers)
            {
                result.Add(new V1Container
                {
                    Name = container.Name,
                    Image = dockerImage.GetImageTag(),
                    Ports = new List<V1ContainerPort>
                    {
                        new V1ContainerPort
                        {
                            ContainerPort = container.ApiPort,
                            Name = container.ContainerPortName
                        }
                    },
                    Env = dockerImage.CreateEnvironmentVariables(offline, container)
                });
            }
            return result;
        }

        private void DeleteDeployment(CodexNodeGroup online)
        {
            if (online.Deployment == null) return;
            client.DeleteNamespacedDeployment(online.Deployment.Name(), K8sNamespace);
            online.Deployment = null;
        }

        #endregion

        #region Namespace management

        private void EnsureTestNamespace()
        {
            if (IsTestNamespaceOnline()) return;

            var namespaceSpec = new V1Namespace
            {
                ApiVersion = "v1",
                Metadata = new V1ObjectMeta
                {
                    Name = K8sNamespace,
                    Labels = new Dictionary<string, string> { { "name", K8sNamespace } }
                }
            };
            client.CreateNamespace(namespaceSpec);
        }

        private void DeleteNamespace()
        {
            if (IsTestNamespaceOnline())
            {
                client.DeleteNamespace(K8sNamespace, null, null, gracePeriodSeconds: 0);
            }
        }

        #endregion

        private bool IsTestNamespaceOnline()
        {
            return client.ListNamespace().Items.Any(n => n.Metadata.Name == K8sNamespace);
        }
    }
}