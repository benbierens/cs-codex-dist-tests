﻿using CodexContractsPlugin;
using GethPlugin;
using Newtonsoft.Json;

namespace BiblioTech
{
    public class UserRepo
    {
        private readonly object repoLock = new object();

        public bool AssociateUserWithAddress(ulong discordId, EthAddress address)
        {
            lock (repoLock)
            {
                return SetUserAddress(discordId, address);
            }
        }

        public void ClearUserAssociatedAddress(ulong discordId)
        {
            lock (repoLock)
            {
                SetUserAddress(discordId, null);
            }
        }

        public void AddMintEventForUser(ulong discordId, EthAddress usedAddress, Ether eth, TestToken tokens)
        {
            lock (repoLock)
            {
                var user = GetOrCreate(discordId);
                user.MintEvents.Add(new UserMintEvent(DateTime.UtcNow, usedAddress, eth, tokens));
                SaveUser(user);
            }
        }

        public EthAddress? GetCurrentAddressForUser(ulong discordId)
        {
            lock (repoLock)
            {
                return GetOrCreate(discordId).CurrentAddress;
            }
        }

        public string[] GetInteractionReport(ulong discordId)
        {
            var result = new List<string>();

            lock (repoLock)
            {
                var filename = GetFilename(discordId);
                if (!File.Exists(filename))
                {
                    result.Add("User has not joined the test net.");
                }
                else
                {
                    var user = JsonConvert.DeserializeObject<User>(File.ReadAllText(filename));
                    if (user == null)
                    {
                        result.Add("Failed to load user records.");
                    }
                    else
                    {
                        result.Add("User joined on " + user.CreatedUtc.ToString("o"));
                        result.Add("Current address: " + user.CurrentAddress);
                        foreach (var ae in user.AssociateEvents)
                        {
                            result.Add($"{ae.Utc.ToString("o")} - Address set to: {ae.NewAddress}");
                        }
                        foreach (var me in user.MintEvents)
                        {
                            result.Add($"{me.Utc.ToString("o")} - Minted {me.EthReceived} and {me.TestTokensMinted} to {me.UsedAddress}.");
                        }
                    }
                }
            }

            return result.ToArray();
        }

        private bool SetUserAddress(ulong discordId, EthAddress? address)
        {
            if (IsAddressUsed(address))
            {
                return false;
            }

            var user = GetOrCreate(discordId);
            user.CurrentAddress = address;
            user.AssociateEvents.Add(new UserAssociateAddressEvent(DateTime.UtcNow, address));
            SaveUser(user);
            return true;
        }

        private User GetOrCreate(ulong discordId)
        {
            var filename = GetFilename(discordId);
            if (!File.Exists(filename))
            {
                return CreateAndSaveNewUser(discordId);
            }
            return JsonConvert.DeserializeObject<User>(File.ReadAllText(filename))!;
        }

        private User CreateAndSaveNewUser(ulong discordId)
        {
            var newUser = new User(discordId, DateTime.UtcNow, null, new List<UserAssociateAddressEvent>(), new List<UserMintEvent>());
            SaveUser(newUser);
            return newUser;
        }

        private bool IsAddressUsed(EthAddress? address)
        {
            if (address == null) return false;

            // If this becomes a performance problem, switch to in-memory cached list.
            var files = Directory.GetFiles(Program.Config.UserDataPath);
            foreach (var file in files)
            {
                try
                {
                    var user = JsonConvert.DeserializeObject<User>(File.ReadAllText(file))!;
                    if (user.CurrentAddress != null &&
                        user.CurrentAddress.Address == address.Address)
                    {
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private void SaveUser(User user)
        {
            var filename = GetFilename(user.DiscordId);
            if (File.Exists(filename)) File.Delete(filename);
            File.WriteAllText(filename, JsonConvert.SerializeObject(user));
        }

        private static string GetFilename(ulong discordId)
        {
            return Path.Combine(Program.Config.UserDataPath, discordId.ToString() + ".json");
        }
    }

    public class User
    {
        public User(ulong discordId, DateTime createdUtc, EthAddress? currentAddress, List<UserAssociateAddressEvent> associateEvents, List<UserMintEvent> mintEvents)
        {
            DiscordId = discordId;
            CreatedUtc = createdUtc;
            CurrentAddress = currentAddress;
            AssociateEvents = associateEvents;
            MintEvents = mintEvents;
        }

        public ulong DiscordId { get; }
        public DateTime CreatedUtc { get; }
        public EthAddress? CurrentAddress { get; set; }
        public List<UserAssociateAddressEvent> AssociateEvents { get; }
        public List<UserMintEvent> MintEvents { get; }
    }

    public class UserAssociateAddressEvent
    {
        public UserAssociateAddressEvent(DateTime utc, EthAddress? newAddress)
        {
            Utc = utc;
            NewAddress = newAddress;
        }

        public DateTime Utc { get; }
        public EthAddress? NewAddress { get; }
    }

    public class UserMintEvent
    {
        public UserMintEvent(DateTime utc, EthAddress usedAddress, Ether ethReceived, TestToken testTokensMinted)
        {
            Utc = utc;
            UsedAddress = usedAddress;
            EthReceived = ethReceived;
            TestTokensMinted = testTokensMinted;
        }

        public DateTime Utc { get; }
        public EthAddress UsedAddress { get; }
        public Ether EthReceived { get; }
        public TestToken TestTokensMinted { get; }
    }
}
