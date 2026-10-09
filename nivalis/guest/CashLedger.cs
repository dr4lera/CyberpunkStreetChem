using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nivalis;
using NivalisModKit;

namespace NivalisNightCity;

internal sealed class CashReceipt
{
    public string Id { get; set; } = "";
    public string Pair { get; set; } = "";
    public string Status { get; set; } = "pending";
    public int NetEddies { get; set; }
    public int GiveEddies { get; set; }
    public int BudgetEddies { get; set; }
    public int Day { get; set; }
}

// Nivalis keeps its native shadow wallet for service, wages and rent. Only changes
// after sandbox preparation belong to Cyberpunk. Whole eddies are settled; fractions
// remain in the baseline. Ingredient purchases restore this wallet, avoiding double charging.
internal static class CashLedger
{
    internal static void Initialize()
    {
        var store = SaveData.For(Plugin.Id);
        if (!store.Get("cash-initialized", false))
        {
            store.Set("cash-baseline", Economy.PlayerMoney ?? 0);
            store.Set("cash-initialized", true);
            store.Set("last-cash-day", GameTime.Day);
        }
    }

    internal static int Delta()
    {
        var store = SaveData.For(Plugin.Id);
        if (!store.Get("cash-initialized", false)) return 0;
        return (int)Math.Clamp(((long)(Economy.PlayerMoney ?? 0) - store.Get("cash-baseline", 0)) / 100,
            -20_000_000L, 20_000_000L);
    }

    internal static CashReceipt Receipt(string id) => SaveData.For(Plugin.Id)
        .Get("cash-receipts", new Dictionary<string, CashReceipt>()).TryGetValue(id, out var r)
        ? r : new CashReceipt { Id = id, Status = "needs_recovery" };

    internal static CashReceipt Settle(string id, string pair, int budget, string save)
    {
        if (!Guid.TryParse(id, out _) || !Guid.TryParse(pair, out _) || budget < 0 || budget > 20_000_000)
            throw new ArgumentException("invalid cash transaction");
        var store = SaveData.For(Plugin.Id);
        if (!store.Get("cash-initialized", false)) throw new InvalidOperationException("prepare the sandbox first");
        var receipts = store.Get("cash-receipts", new Dictionary<string, CashReceipt>());
        if (receipts.TryGetValue(id, out var old)) return old;
        if (receipts.Values.Any(r => r.Status != "committed")) throw new InvalidOperationException("earlier cash transfer needs recovery");
        var bound = store.Get("financial-pair", "");
        if (bound != "" && bound != pair) throw new InvalidOperationException("different Cyberpunk financial save");
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RestockPurchaser.HostPath,
            "red4ext", "plugins", "NivalisNightCity", "transactions", id + ".json")));
        var ticket = doc.RootElement;
        if (ticket.GetProperty("kind").GetString() != "cash" || ticket.GetProperty("status").GetString() != "reserved" ||
            ticket.GetProperty("pair").GetString() != pair || ticket.GetProperty("budget").GetInt32() != budget ||
            ticket.GetProperty("before").GetInt32() - ticket.GetProperty("after").GetInt32() != budget)
            throw new InvalidOperationException("host cash reservation required");
        int net = Math.Max(-budget, Delta());
        var receipt = new CashReceipt { Id = id, Pair = pair, NetEddies = net,
            GiveEddies = checked(budget + net), BudgetEddies = budget, Day = GameTime.Day };
        receipts[id] = receipt;
        store.Set("financial-pair", pair);
        store.Set("cash-receipts", receipts);
        if (!Singleton<SerializationManager>.Instance.Save(save, false)) throw new InvalidOperationException("pending cash checkpoint failed");
        store.Set("cash-baseline", checked(store.Get("cash-baseline", 0) + net * 100));
        store.Set("last-cash-day", GameTime.Day);
        receipt.Status = "committed";
        receipts[id] = receipt;
        store.Set("cash-receipts", receipts);
        if (!Singleton<SerializationManager>.Instance.Save(save, false))
        {
            receipt.Status = "needs_recovery";
            receipts[id] = receipt; store.Set("cash-receipts", receipts);
            throw new InvalidOperationException("cash checkpoint failed; do not replay");
        }
        return receipt;
    }
}
