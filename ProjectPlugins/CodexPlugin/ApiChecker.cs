using Core;
using KubernetesWorkflow.Types;
using Logging;
using System.Security.Cryptography;
using System.Text;
using Utils;

namespace CodexPlugin
{
    public class ApiChecker
    {
        // <INSERT-OPENAPI-YAML-HASH>
        private const string OpenApiYamlHash = "39-0C-32-A3-EA-90-4F-29-1C-67-12-F1-D5-BE-31-67-8D-90-43-1E-F2-02-63-5B-0C-49-F7-1E-E5-EC-F7-00";
        private const string OpenApiFilePath = "/codex/openapi.yaml";
        private const string DisableEnvironmentVariable = "CODEXPLUGIN_DISABLE_APICHECK";

        private const bool Disable = false;

        private const string Warning =
            "Warning: CodexPlugin was unable to find the openapi.yaml file in the Codex container. Are you running an old version of Codex? " +
            "Plugin will continue as normal, but API compatibility is not guaranteed!";

        private const string Failure =
            "Codex API compatibility check failed! " +
            "openapi.yaml used by CodexPlugin does not match openapi.yaml in Codex container. The openapi.yaml in " +
            "'ProjectPlugins/CodexPlugin' has been overwritten with the container one. " +
            "Please and rebuild this project. If you wish to disable API compatibility checking, please set " +
            $"the environment variable '{DisableEnvironmentVariable}' or set the disable bool in 'ProjectPlugins/CodexPlugin/ApiChecker.cs'.";

        private static bool checkPassed = false;

        private readonly IPluginTools pluginTools;
        private readonly ILog log;

        public ApiChecker(IPluginTools pluginTools)
        {
            this.pluginTools = pluginTools;
            log = pluginTools.GetLog();

            if (string.IsNullOrEmpty(OpenApiYamlHash)) throw new Exception("OpenAPI yaml hash was not inserted by pre-build trigger.");
        }

        public void CheckCompatibility(RunningPod[] containers)
        {
            if (checkPassed) return;

            Log("CodexPlugin is checking API compatibility...");

            if (Disable || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DisableEnvironmentVariable)))
            {
                Log("API compatibility checking has been disabled.");
                checkPassed = true;
                return;
            }

            var workflow = pluginTools.CreateWorkflow();
            var container = containers.First().Containers.First();
            var containerApi = workflow.ExecuteCommand(container, "cat", OpenApiFilePath);

            if (string.IsNullOrEmpty(containerApi))
            {
                log.Error(Warning);

                checkPassed = true;
                return;
            }

            var containerHash = Hash(containerApi);
            if (containerHash == OpenApiYamlHash)
            {
                Log("API compatibility check passed.");
                checkPassed = true;
                return;
            }

            OverwriteOpenApiYaml(containerApi);

            log.Error(Failure);
            throw new Exception(Failure);
        }

        private void OverwriteOpenApiYaml(string containerApi)
        {
            Log("API compatibility check failed. Updating CodexPlugin...");
            var openApiFilePath = Path.Combine(PluginPathUtils.ProjectPluginsDir, "CodexPlugin", "openapi.yaml");
            if (!File.Exists(openApiFilePath)) throw new Exception("Unable to locate CodexPlugin/openapi.yaml. Expected: " + openApiFilePath);

            File.Delete(openApiFilePath);
            File.WriteAllText(openApiFilePath, containerApi);
            Log("CodexPlugin/openapi.yaml has been updated.");
        }

        private string Hash(string file)
        {
            var fileBytes = Encoding.ASCII.GetBytes(file
                .Replace(Environment.NewLine, ""));
            var sha = SHA256.Create();
            var hash = sha.ComputeHash(fileBytes);
            return BitConverter.ToString(hash);
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }
    }
}
