# Distributed System Tests for Nim-Codex


Using a common dotnet unit-test framework and a few other libraries, this project allows you to write tests that use multiple Codex node instances in various configurations to test the distributed system in a controlled, reproducible environment.

Nim-Codex: https://github.com/codex-storage/nim-codex  
Dotnet: v6.0  
Kubernetes: v1.25.4  
Dotnet-kubernetes SDK: v10.1.4 https://github.com/kubernetes-client/csharp  
Nethereum: v4.14.0

## Tests
Tests are devided into two assemblies: `/Tests` and `/LongTests`.
`/Tests` is to be used for tests that take several minutes to hours to execute.
`/LongTests` is to be used for tests that take hours to days to execute.

TODO: All tests will eventually be running as part of a dedicated CI pipeline and kubernetes cluster. Currently, we're developing these tests and the infra-code to support it by running the whole thing locally.

## Configuration
Test executing can be configured using the following environment variables.
| Variable       | Description                                                                                                                                          | Default             |
|----------------|------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------|
| KUBECONFIG     | Optional path (abs or rel) to kubeconfig YAML file. When null, uses system default (docker-desktop) kubeconfig if available.                         | (null)              |
| LOGPATH        | Path (abs or rel) where log files will be saved.                                                                                                     | "CodexTestLogs"     |
| LOGDEBUG       | When "true", enables additional test-runner debug log output.                                                                                        | "false"             |
| DATAFILEPATH   | Path (abs or rel) where temporary test data files will be saved.                                                                                     | "TestDataFiles"     |
| LOGLEVEL       | Codex log-level. (case-insensitive)                                                                                                                  | "Trace"             |
| RUNNERLOCATION | Use "ExternalToCluster" when test app is running outside of the k8s cluster. Use "InternalToCluster" when tests are run from inside a pod/container. | "ExternalToCluster" |

## Test logs
Because tests potentially take a long time to run, logging is in place to help you investigate failures afterwards. Should a test fail, all Codex terminal output (as well as metrics if they have been enabled) will be downloaded and stored along with a detailed, step-by-step log of the test. If something's gone wrong and you're here to discover the details, head for the logs.

## How to contribute tests
An important goal of the test infra is to provide a simple, accessible way for developers to write their tests. If you want to contribute tests for Codex, please follow the steps [HERE](/CONTRIBUTINGTESTS.md).

## Run the tests on your machine
Creating tests is much easier when you can debug them on your local system. This is possible, but requires some set-up. If you want to be able to run the tests on your local system, follow the steps [HERE](/docs/LOCALSETUP.md). Please note that tests which require explicit node locations cannot be executed locally. (Well, you could comment out the location statements and then it would probably work. But that might impact the validity/usefulness of the test.)

## Missing functionality
Surely the test-infra doesn't do everything we'll need it to do. If you're running into a limitation and would like to request a new feature for the test-infra, please create an issue.
