﻿using ArgsUniform;
using CodexNetDeployer;
using DistTestCore;
using Newtonsoft.Json;
using Configuration = CodexNetDeployer.Configuration;

public class Program
{
    public static void Main(string[] args)
    {
        var nl = Environment.NewLine;
        Console.WriteLine("CodexNetDeployer" + nl);

        if (args.Any(a => a == "-h" || a == "--help" || a == "-?"))
        {
            PrintHelp();
            return;
        }

        var uniformArgs = new ArgsUniform<Configuration>(new Configuration.Defaults(), args);
        var config = uniformArgs.Parse(true);
        
        if (args.Any(a => a == "--external"))
        {
            config.RunnerLocation = TestRunnerLocation.ExternalToCluster;
        }

        var errors = config.Validate();
        if (errors.Any())
        {
            Console.WriteLine($"Configuration errors: ({errors.Count})");
            foreach ( var error in errors ) Console.WriteLine("\t" + error);
            Console.WriteLine(nl);
            PrintHelp();
            return;
        }

        var deployer = new Deployer(config);
        var deployment = deployer.Deploy();

        Console.WriteLine("Writing codex-deployment.json...");

        File.WriteAllText("codex-deployment.json", JsonConvert.SerializeObject(deployment, Formatting.Indented));

        Console.WriteLine("Done!");
    }

    private static void PrintHelp()
    {
        var nl = Environment.NewLine;
        Console.WriteLine("CodexNetDeployer allows you to easily deploy multiple Codex nodes in a Kubernetes cluster. " +
            "The deployer will set up the required supporting services, deploy the Codex on-chain contracts, start and bootstrap the Codex instances. " +
            "All Kubernetes objects will be created in the namespace provided, allowing you to easily find, modify, and delete them afterwards." + nl);

        Console.WriteLine("CodexNetDeployer assumes you are running this tool from *inside* the Kubernetes cluster you want to deploy to. " +
            "If you are not running this from a container inside the cluster, add the argument '--external'." + nl);

        var uniformArgs = new ArgsUniform<Configuration>();
        uniformArgs.PrintHelp();
    }
}
