﻿using Logging;

namespace AutoClient.Modes
{
    public class PurchasingMode : IMode
    {
        private readonly List<AutomaticPurchaser> purchasers = new List<AutomaticPurchaser>();
        private readonly App app;
        private Task starterTask = Task.CompletedTask;

        public PurchasingMode(App app)
        {
            this.app = app;
        }

        public void Start(ICodexInstance instance, int index)
        {
            for (var i = 0; i < app.Config.NumConcurrentPurchases; i++)
            {
                purchasers.Add(new AutomaticPurchaser(new LogPrefixer(app.Log, $"({i}) "), instance, new CodexNode(app, instance)));
            }

            var delayPerPurchaser =
                TimeSpan.FromSeconds(10 * index) +
                TimeSpan.FromMinutes(app.Config.ContractDurationMinutes) / app.Config.NumConcurrentPurchases;

            starterTask = Task.Run(() => StartPurchasers(delayPerPurchaser));
        }

        private async Task StartPurchasers(TimeSpan delayPerPurchaser)
        {
            foreach (var purchaser in purchasers)
            {
                purchaser.Start();
                await Task.Delay(delayPerPurchaser);
            }
        }

        public void Stop()
        {
            starterTask.Wait();
            foreach (var purchaser in purchasers)
            {
                purchaser.Stop();
            }
        }
    }
}
