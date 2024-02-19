﻿using Logging;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Utils;

namespace NethereumWorkflow
{
    public partial class BlockTimeFinder
    {
        private const ulong FetchRange = 6;
        private const int MaxEntries = 1024;
        private static readonly Dictionary<ulong, BlockTimeEntry> entries = new Dictionary<ulong, BlockTimeEntry>();
        private readonly Web3 web3;
        private readonly ILog log;
        
        public BlockTimeFinder(Web3 web3, ILog log)
        {
            this.web3 = web3;
            this.log = log;
        }

        public ulong GetHighestBlockNumberBefore(DateTime moment)
        {
            log.Log("Looking for highest block before " + moment.ToString("o"));
            AssertMomentIsInPast(moment);
            Initialize();

            return GetHighestBlockBefore(moment);
        }

        public ulong GetLowestBlockNumberAfter(DateTime moment)
        {
            log.Log("Looking for lowest block after " + moment.ToString("o"));
            AssertMomentIsInPast(moment);
            Initialize();

            return GetLowestBlockAfter(moment);
        }

        private ulong GetHighestBlockBefore(DateTime moment)
        {
            var closestBefore = FindClosestBeforeEntry(moment);
            var closestAfter = FindClosestAfterEntry(moment);

            if (closestBefore != null &&
                closestAfter != null &&
                closestBefore.Utc < moment &&
                closestAfter.Utc > moment &&
                closestBefore.BlockNumber + 1 == closestAfter.BlockNumber)
            {
                log.Log("Found highest-Before: " + closestBefore);
                return closestBefore.BlockNumber;
            }

            var newBlocks = FetchBlocksAround(moment);
            if (newBlocks == 0)
            {
                log.Log("Didn't find any new blocks.");
                if (closestBefore != null) return closestBefore.BlockNumber;
                throw new Exception("Failed to find highest before.");
            }
            return GetHighestBlockBefore(moment);
        }

        private ulong GetLowestBlockAfter(DateTime moment)
        {
            var closestBefore = FindClosestBeforeEntry(moment);
            var closestAfter = FindClosestAfterEntry(moment);

            if (closestBefore != null &&
                closestAfter != null &&
                closestBefore.Utc < moment &&
                closestAfter.Utc > moment &&
                closestBefore.BlockNumber + 1 == closestAfter.BlockNumber)
            {
                log.Log("Found lowest-after: " + closestAfter);
                return closestAfter.BlockNumber;
            }

            var newBlocks = FetchBlocksAround(moment);
            if (newBlocks == 0)
            {
                log.Log("Didn't find any new blocks.");
                if (closestAfter != null) return closestAfter.BlockNumber;
                throw new Exception("Failed to find lowest before.");
            }
            return GetLowestBlockAfter(moment);
        }

        private int FetchBlocksAround(DateTime moment)
        {
            var timePerBlock = EstimateTimePerBlock();
            log.Debug("Fetching blocks around " + moment.ToString("o") + " timePerBlock: " + timePerBlock.TotalSeconds);

            EnsureRecentBlockIfNecessary(moment, timePerBlock);

            var max = entries.Keys.Max();
            var blockDifference = CalculateBlockDifference(moment, timePerBlock, max);

            return
                FetchUp(max, blockDifference) +
                FetchDown(max, blockDifference);
        }

        private int FetchDown(ulong max, ulong blockDifference)
        {
            var target = GetTarget(max, blockDifference);
            var fetchDown = FetchRange;
            var newBlocks = 0;
            while (fetchDown > 0)
            {
                if (!entries.ContainsKey(target))
                {
                    var newBlock = AddBlockNumber("FD" + fetchDown, target);
                    if (newBlock == null) return newBlocks;
                    newBlocks++;
                    fetchDown--;
                }
                target--;
                if (target <= 0) return newBlocks;
            }
            return newBlocks;
        }

        private int FetchUp(ulong max, ulong blockDifference)
        {
            var target = GetTarget(max, blockDifference);
            var fetchUp = FetchRange;
            var newBlocks = 0;
            while (fetchUp > 0)
            {
                if (!entries.ContainsKey(target))
                {
                    var newBlock = AddBlockNumber("FU" + fetchUp, target);
                    if (newBlock == null) return newBlocks;
                    newBlocks++;
                    fetchUp--;
                }
                target++;
                if (target >= max) return newBlocks;
            }
            return newBlocks;
        }

        private ulong GetTarget(ulong max, ulong blockDifference)
        {
            if (max <= blockDifference) return 1;
            return max - blockDifference;
        }

        private ulong CalculateBlockDifference(DateTime moment, TimeSpan timePerBlock, ulong max)
        {
            var latest = entries[max];
            var timeDifference = latest.Utc - moment;
            double secondsDifference = Math.Abs(timeDifference.TotalSeconds);
            double secondsPerBlock = timePerBlock.TotalSeconds;

            double numberOfBlocksDifference = secondsDifference / secondsPerBlock;
            var blockDifference = Convert.ToUInt64(numberOfBlocksDifference);
            if (blockDifference < 1) blockDifference = 1;
            return blockDifference;
        }

        private void EnsureRecentBlockIfNecessary(DateTime moment, TimeSpan timePerBlock)
        {
            var max = entries.Keys.Max();
            var latest = entries[max];
            var maxRetry = 10;
            while (moment > latest.Utc)
            {
                var newBlock = AddCurrentBlock();
                if (newBlock == null || newBlock.BlockNumber == latest.BlockNumber)
                {
                    maxRetry--;
                    if (maxRetry == 0) throw new Exception("Unable to fetch recent block after 10x tries.");
                    Thread.Sleep(timePerBlock);
                }
                max = entries.Keys.Max();
                latest = entries[max];
            }
        }

        private BlockTimeEntry? AddBlockNumber(string a, decimal blockNumber)
        {
            return AddBlockNumber(a, Convert.ToUInt64(blockNumber));
        }

        private BlockTimeEntry? AddBlockNumber(string a, ulong blockNumber)
        {
            log.Log(a + " - Adding blockNumber: " + blockNumber);
            if (entries.ContainsKey(blockNumber))
            {
                return entries[blockNumber];
            }

            if (entries.Count > MaxEntries)
            {
                log.Debug("Entries cleared!");
                entries.Clear();
                Initialize();
            }

            var time = GetTimestampFromBlock(blockNumber);
            if (time == null)
            {
                log.Log("Failed to get block for number: " + blockNumber);
                return null;
            }
            var entry = new BlockTimeEntry(blockNumber, time.Value);
            log.Debug("Found block " + entry.BlockNumber + " at " + entry.Utc.ToString("o"));
            entries.Add(blockNumber, entry);
            return entry;
        }

        private TimeSpan EstimateTimePerBlock()
        {
            var min = entries.Keys.Min();
            var max = entries.Keys.Max();
            log.Log("min/max: " + min + " / " + max);
            var clippedMin = Math.Max(max - 100, min);
            var minTime = entries[min].Utc;
            var clippedMinBlock = AddBlockNumber("EST", clippedMin);
            if (clippedMinBlock != null) minTime = clippedMinBlock.Utc;

            var maxTime = entries[max].Utc;
            var elapsedTime = maxTime - minTime;

            double elapsedSeconds = elapsedTime.TotalSeconds;
            double numberOfBlocks = max - min;
            double secondsPerBlock = elapsedSeconds / numberOfBlocks;

            var result = TimeSpan.FromSeconds(secondsPerBlock);
            if (result.TotalSeconds < 1.0) result = TimeSpan.FromSeconds(1.0);
            return result;
        }

        private void Initialize()
        {
            if (!entries.Any())
            {
                AddCurrentBlock();
                AddBlockNumber("INIT", entries.Single().Key - 1);
            }
        }

        private static void AssertMomentIsInPast(DateTime moment)
        {
            if (moment > DateTime.UtcNow) throw new Exception("Moment must be UTC and must be in the past.");
        }

        private BlockTimeEntry? AddCurrentBlock()
        {
            var number = Time.Wait(web3.Eth.Blocks.GetBlockNumber.SendRequestAsync());
            var blockNumber = number.ToDecimal();
            return AddBlockNumber("CUR", blockNumber);
        }

        private DateTime? GetTimestampFromBlock(ulong blockNumber)
        {
            try
            {
                var block = Time.Wait(web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new BlockParameter(blockNumber)));
                if (block == null) return null;
                return DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(block.Timestamp.ToDecimal())).UtcDateTime;
            }
            catch (Exception ex)
            {
                log.Error(nameof(GetTimestampFromBlock) + " Exception: " + ex);
                throw;
            }
        }

        private BlockTimeEntry? FindClosestBeforeEntry(DateTime moment)
        {
            BlockTimeEntry? result = null;
            foreach (var entry in entries.Values)
            {
                if (result == null)
                {
                    if (entry.Utc < moment) result = entry;
                }
                else
                {
                    if (entry.Utc > result.Utc && entry.Utc < moment) result = entry;
                }
            }
            return result;
        }

        private BlockTimeEntry? FindClosestAfterEntry(DateTime moment)
        {
            BlockTimeEntry? result = null;
            foreach (var entry in entries.Values)
            {
                if (result == null)
                {
                    if (entry.Utc > moment) result = entry;
                }
                else
                {
                    if (entry.Utc < result.Utc && entry.Utc > moment) result = entry;
                }
            }
            return result;
        }
    }
}
