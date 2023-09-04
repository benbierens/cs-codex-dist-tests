﻿using DistTestCore.Codex;
using Logging;
using NUnit.Framework;
using Utils;

namespace DistTestCore.Helpers
{
    public interface IFullConnectivityImplementation
    {
        string Description();
        string ValidateEntry(FullConnectivityHelper.Entry entry, FullConnectivityHelper.Entry[] allEntries);
        FullConnectivityHelper.PeerConnectionState Check(FullConnectivityHelper.Entry from, FullConnectivityHelper.Entry to);
    }

    public class FullConnectivityHelper
    {
        private static string Nl = Environment.NewLine;
        private readonly BaseLog log;
        private readonly IFullConnectivityImplementation implementation;

        public FullConnectivityHelper(BaseLog log, IFullConnectivityImplementation implementation)
        {
            this.log = log;
            this.implementation = implementation;
        }

        public void AssertFullyConnected(IEnumerable<CodexAccess> nodes)
        {
            AssertFullyConnected(nodes.ToArray());
        }

        private void AssertFullyConnected(CodexAccess[] nodes)
        {
            Log($"Asserting '{implementation.Description()}' for nodes: '{string.Join(",", nodes.Select(n => n.GetName()))}'...");
            var entries = CreateEntries(nodes);
            var pairs = CreatePairs(entries);

            RetryWhilePairs(pairs, () =>
            {
                CheckAndRemoveSuccessful(pairs);
            });

            if (pairs.Any())
            {
                var pairDetails = string.Join(Nl, pairs.SelectMany(p => p.GetResultMessages()));

                Log($"Connections failed:{Nl}{pairDetails}");

                Assert.Fail(string.Join(Nl, pairs.SelectMany(p => p.GetResultMessages())));
            }
            else
            {
                Log($"'{implementation.Description()}' = Success! for nodes: {string.Join(",", nodes.Select(n => n.GetName()))}");
            }
        }

        private static void RetryWhilePairs(List<Pair> pairs, Action action)
        {
            var timeout = DateTime.UtcNow + TimeSpan.FromMinutes(2);
            while (pairs.Any(p => p.Inconclusive) && timeout > DateTime.UtcNow)
            {
                action();

                Time.Sleep(TimeSpan.FromSeconds(2));
            }
        }

        private void CheckAndRemoveSuccessful(List<Pair> pairs)
        {
            // For large sets, don't try and do all of them at once.
            var selectedPair = pairs.Take(20).ToArray();
            var pairDetails = new List<string>();

            foreach (var pair in selectedPair)
            {
                pair.Check();

                if (pair.Success)
                {
                    pairDetails.AddRange(pair.GetResultMessages());
                    pairs.Remove(pair);
                }
            }

            Log($"Connections successful:{Nl}{string.Join(Nl, pairDetails)}");
        }

        private Entry[] CreateEntries(CodexAccess[] nodes)
        {
            var entries = nodes.Select(n => new Entry(n)).ToArray();

            var errors = entries
                            .Select(e => implementation.ValidateEntry(e, entries))
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToArray();

            if (errors.Any())
            {
                Assert.Fail("Some node entries failed to validate: " + string.Join(Nl, errors));
            }

            return entries;
        }

        private List<Pair> CreatePairs(Entry[] entries)
        {
            return CreatePairsIterator(entries).ToList();
        }

        private IEnumerable<Pair> CreatePairsIterator(Entry[] entries)
        {
            for (var x = 0; x < entries.Length; x++)
            {
                for (var y = x + 1; y < entries.Length; y++)
                {
                    yield return new Pair(implementation, entries[x], entries[y]);
                }
            }
        }

        private void Log(string msg)
        {
            log.Log(msg);
        }

        public class Entry
        {
            public Entry(CodexAccess node)
            {
                Node = node;
                Response = node.GetDebugInfo();
            }

            public CodexAccess Node { get; }
            public CodexDebugResponse Response { get; }

            public override string ToString()
            {
                if (Response == null || string.IsNullOrEmpty(Response.id)) return "UNKNOWN";
                return Response.id;
            }
        }

        public enum PeerConnectionState
        {
            Unknown,
            Connection,
            NoConnection,
        }

        public class Pair
        {
            private TimeSpan aToBTime = TimeSpan.FromSeconds(0);
            private TimeSpan bToATime = TimeSpan.FromSeconds(0);
            private readonly IFullConnectivityImplementation implementation;

            public Pair(IFullConnectivityImplementation implementation, Entry a, Entry b)
            {
                this.implementation = implementation;
                A = a;
                B = b;
            }

            public Entry A { get; }
            public Entry B { get; }
            public PeerConnectionState AKnowsB { get; private set; }
            public PeerConnectionState BKnowsA { get; private set; }
            public bool Success { get { return AKnowsB == PeerConnectionState.Connection && BKnowsA == PeerConnectionState.Connection; } }
            public bool Inconclusive { get { return AKnowsB == PeerConnectionState.Unknown || BKnowsA == PeerConnectionState.Unknown; } }

            public void Check()
            {
                aToBTime = Measure(() => AKnowsB = Check(A, B));
                bToATime = Measure(() => BKnowsA = Check(B, A));
            }

            public override string ToString()
            {
                return $"[{string.Join(",", GetResultMessages())}]";
            }

            public string[] GetResultMessages()
            {
                var aName = A.ToString();
                var bName = B.ToString();

                return new[]
                {
                    $"[{aName} --> {bName}] = {AKnowsB} ({aToBTime.TotalSeconds} seconds)",
                    $"[{aName} <-- {bName}] = {BKnowsA} ({bToATime.TotalSeconds} seconds)"
                };
            }

            private static TimeSpan Measure(Action action)
            {
                var start = DateTime.UtcNow;
                action();
                return DateTime.UtcNow - start;
            }

            private PeerConnectionState Check(Entry from, Entry to)
            {
                Thread.Sleep(10);

                try
                {
                    return implementation.Check(from, to);
                }
                catch
                {
                    // Didn't get a conclusive answer. Try again later.
                    return PeerConnectionState.Unknown;
                }
            }
        }
    }
}
