using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class CoopCurrencySource
    {
        public CoopCurrencySource(string id, string currency, int amount, int dailyCap, int perFriendDailyCap)
        {
            Id = id;
            Currency = currency;
            Amount = amount;
            DailyCap = dailyCap;
            PerFriendDailyCap = perFriendDailyCap;
        }

        public string Id { get; }
        public string Currency { get; }
        public int Amount { get; }
        public int DailyCap { get; }
        public int PerFriendDailyCap { get; }
    }

    public sealed class CoopCurrencyRules
    {
        public const string FileName = "coop_currency.json";
        public const int SupportedVersion = 1;
        public const string Friendship = "friendship";
        public const string Seals = "seals";

        private CoopCurrencyRules(IReadOnlyList<string> currencies, int dayUtcOffsetMinutes, int ledgerSize, IReadOnlyList<CoopCurrencySource> sources)
        {
            Currencies = currencies;
            DayUtcOffsetMinutes = dayUtcOffsetMinutes;
            LedgerSize = ledgerSize;
            Sources = sources;
        }

        public IReadOnlyList<string> Currencies { get; }
        public int DayUtcOffsetMinutes { get; }
        public int LedgerSize { get; }
        public IReadOnlyList<CoopCurrencySource> Sources { get; }

        public static CoopCurrencyRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var currencies = new List<string>();
            var currencyNode = root.Field("currencies");
            foreach (var item in currencyNode.Items())
            {
                var currency = item.AsString();
                if (currency != Friendship && currency != Seals)
                    throw item.Error($"unknown currency \"{currency}\"");
                if (currencies.Contains(currency))
                    throw item.Error($"\"{currency}\" is repeated");
                currencies.Add(currency);
            }
            if (!currencies.Contains(Friendship) || !currencies.Contains(Seals))
                throw currencyNode.Error($"must list \"{Friendship}\" and \"{Seals}\"");
            var sources = new List<CoopCurrencySource>();
            var ids = new HashSet<string>();
            foreach (var item in root.Field("sources").Items())
            {
                var id = item.Field("id").AsString();
                if (id.Length == 0 || !ids.Add(id))
                    throw item.Field("id").Error("must be a new, non-empty id");
                var currency = item.Field("currency").AsString();
                if (!currencies.Contains(currency))
                    throw item.Field("currency").Error($"\"{currency}\" is not one of currencies");
                var amount = item.Field("amount").AsInt(1, 1000);
                var dailyCap = item.Field("dailyCap").AsInt(amount, 100000);
                var perFriend = item.HasField("perFriendDailyCap") ? item.Field("perFriendDailyCap").AsInt(1, dailyCap) : 0;
                sources.Add(new CoopCurrencySource(id, currency, amount, dailyCap, perFriend));
            }
            return new CoopCurrencyRules(currencies, root.Field("dayUtcOffsetMinutes").AsInt(-720, 840), root.Field("ledgerSize").AsInt(1, 200), sources);
        }
    }
}
