using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nivalis;
using Nivalis.Economy;
using Nivalis.GhostSystem.CustomerLoop;
using Nivalis.InventorySystem;
using NivalisModKit;

namespace NivalisNightCity;

internal sealed class RestockReceipt
{
    public string Id { get; set; } = "";
    public string Pair { get; set; } = "";
    public int Day { get; set; }
    public int BudgetEddies { get; set; }
    public int CostHundredths { get; set; }
    public int ChargeEddies { get; set; }
    public int RefundEddies { get; set; }
    public int CreditHundredths { get; set; }
    public int Bought { get; set; }
    public string Status { get; set; } = "pending";
    public List<string> Shortages { get; set; } = new();
    public List<SupplyLine> Purchases { get; set; } = new();
}

internal sealed record SupplyLine(string Venue, string Item, string Vendor, int Requested,
    int Bought, int UnitPriceHundredths, int ExpectedHundredths, int PaidHundredths, int ObservedIncrease);

internal static class RestockPurchaser
{
    internal static string HostPath = "";
    private sealed record Target(ItemType Item, int Missing, VenueAreaGhost? Venue);
    private sealed class Job
    {
        internal RestockReceipt Receipt = null!;
        internal List<Target> Targets = null!;
        internal int Index, Remaining, Credit;
        internal string Save = "";
    }
    private static Job? active;
    internal static bool Busy => active != null;
    internal static void Cancel() => active = null;
    internal static RestockReceipt Read(string id)
    {
        var receipts = SaveData.For(Plugin.Id).Get("restock-receipts", new Dictionary<string, RestockReceipt>());
        if (!receipts.TryGetValue(id, out var receipt)) return new() { Id = id, Status = "needs_recovery" };
        if (receipt.Status == "pending" && active?.Receipt.Id != id) receipt.Status = "needs_recovery";
        return receipt;
    }

    internal static object Buy(string id, string pair, int budgetEddies, string save)
    {
        if (!Guid.TryParse(id, out _) || !Guid.TryParse(pair, out _)) throw new ArgumentException("transaction and pair IDs must be UUIDs");
        if (budgetEddies <= 0 || budgetEddies > 20_000_000) throw new ArgumentOutOfRangeException(nameof(budgetEddies));
        var store = SaveData.For(Plugin.Id);
        if (Busy) throw new InvalidOperationException("a supply purchase is already running");
        var receipts = store.Get("restock-receipts", new Dictionary<string, RestockReceipt>());
        if (receipts.TryGetValue(id, out var existing)) return existing;
        if (receipts.Values.Any(r => r.Status != "committed"))
            throw new InvalidOperationException("an earlier supply transaction requires recovery; do not reserve more funds");
        var boundPair = store.Get("financial-pair", "");
        if (boundPair != "" && boundPair != pair) throw new InvalidOperationException("supplies are bound to a different Cyberpunk save");
        var reservationPath = Path.Combine(HostPath, "red4ext", "plugins", "NivalisNightCity", "transactions", id + ".json");
        using var reservation = JsonDocument.Parse(File.ReadAllText(reservationPath));
        var ticket = reservation.RootElement;
        if (ticket.GetProperty("status").GetString() != "reserved" || ticket.GetProperty("pair").GetString() != pair ||
            ticket.GetProperty("budget").GetInt32() != budgetEddies || ticket.GetProperty("before").GetInt32() - ticket.GetProperty("after").GetInt32() != budgetEddies)
            throw new InvalidOperationException("matching host eddy reservation required");
        var manager = Singleton<ShoppingListManager>.Instance;
        manager.UpdateListExternal();
        StorageSetup.EnsureMenuCapacity();
        var targets = SupplyTargets.Build().Where(t => t.Missing > 0)
            .Select(t => new Target(t.Item, t.Missing, t.Venue)).ToList();
        var player = Singleton<PlayerManager>.Instance.LocalPlayer;
        var inventory = player.Inventory;
        int credit = store.Get("restock-credit-hundredths", 0);
        if (credit < 0 || credit >= 100) throw new InvalidOperationException("restock credit requires recovery");
        int reserveHundredths = checked(budgetEddies * 100 + credit);
        var receipt = new RestockReceipt { Id = id, Pair = pair, Day = GameTime.Day, BudgetEddies = budgetEddies };
        // Checkpoint the pending identity BEFORE any supplier or inventory mutation.
        // A failed/rolled-back transaction cannot silently be retried under a fresh ID.
        receipts[id] = receipt;
        store.Set("financial-pair", pair);
        store.Set("restock-receipts", receipts);
        if (!Singleton<SerializationManager>.Instance.Save(save, false))
            throw new InvalidOperationException("could not checkpoint pending purchase; no items bought");
        active = new Job { Receipt = receipt, Targets = targets, Remaining = reserveHundredths,
            Credit = credit, Save = save };
        Plugin.Active!.HoldPurchase();
        Scheduler.NextFrame(Step);
        return receipt;
    }

    private static void Step()
    {
        var job = active;
        if (job == null) return;
        var receipt = job.Receipt;
        var store = SaveData.For(Plugin.Id);
        var receipts = store.Get("restock-receipts", new Dictionary<string, RestockReceipt>());
        var player = Singleton<PlayerManager>.Instance.LocalPlayer;
        var inventory = player.Inventory;
        int originalMoney = inventory.Money;
        int startingFunds = job.Remaining;
        inventory.ChangeMoneyWithoutReceipt(startingFunds - originalMoney);
        try
        {
            // Bound each main-thread slice. Full shopping lists can cover hundreds of
            // venue allocations; never hold the game in one large purchase callback.
            int end = Math.Min(job.Index + 8, job.Targets.Count);
            while (job.Index < end)
            {
                var target = job.Targets[job.Index++];
                int remaining = target.Missing;
                var offers = Economy.VendorsFor(target.Item).Where(Economy.IsUnlocked)
                    .Select(v => new { vendor = v, price = Economy.Price(v, target.Item), stock = Economy.Stock(v, target.Item) })
                    .Where(o => o.price.HasValue && o.price.Value >= 0 && o.stock > 0).OrderBy(o => o.price).ToArray();
                foreach (var offer in offers)
                {
                    if (remaining <= 0) break;
                    int amount = Math.Min(remaining, offer.stock);
                    if (target.Venue != null) amount = Math.Min(amount, StorageSetup.Free(target.Venue, target.Item));
                    if (offer.price!.Value > 0) amount = Math.Min(amount, inventory.Money / offer.price.Value);
                    if (amount <= 0) continue;
                    var destination = target.Venue != null ? new IItemContainer(target.Venue.JointInventory.Pointer) : new IItemContainer(inventory.Pointer);
                    int beforeItems = destination.GetItemCount(target.Item, out _);
                    int beforeMoney = inventory.Money;
                    NativePurchase.Buy(offer.vendor, target.Venue?.Pointer ?? player.Pointer,
                        destination.Pointer, target.Item, amount, offer.price.Value);
                    int observed = Math.Max(0, destination.GetItemCount(target.Item, out _) - beforeItems);
                    // Native Refresh may also reveal previously delivered items.
                    // Credit this call with at most its requested quantity.
                    int bought = Math.Min(amount, observed);
                    int paid = beforeMoney - inventory.Money;
                    int deliveredCost = checked(bought * offer.price.Value);
                    // Native venue purchases can accept only part of a stack while
                    // charging for the complete request. Never pass that loss to V.
                    if (paid > deliveredCost) inventory.ChangeMoneyWithoutReceipt(paid - deliveredCost);
                    if (paid < 0) throw new InvalidOperationException("negative supplier charge; recovery required");
                    receipt.Purchases.Add(new SupplyLine(target.Venue?.Venue.name ?? "player", Items.NameOf(target.Item),
                        Economy.NameOf(offer.vendor), amount, bought, offer.price.Value,
                        checked(amount * offer.price.Value), beforeMoney - inventory.Money, observed));
                    receipt.Bought += bought;
                    remaining -= bought;
                }
                if (remaining > 0) receipt.Shortages.Add($"{target.Venue?.Venue.name ?? "player"}: {Items.NameOf(target.Item)} — {remaining} unfilled (funds, supplier stock, or storage)");
            }
            int spent = startingFunds - inventory.Money;
            if (spent < 0 || spent > startingFunds)
                throw new InvalidOperationException("native purchase cost escaped the reserved budget");
            receipt.CostHundredths += spent;
            job.Remaining = inventory.Money;
        }
        catch (Exception error)
        {
            receipt.CostHundredths += Math.Clamp(startingFunds - inventory.Money, 0, startingFunds);
            receipt.Status = "needs_recovery";
            receipt.Shortages.Add(error.Message);
            receipts[receipt.Id] = receipt;
            store.Set("restock-receipts", receipts);
            active = null;
            Scheduler.NextFrame(() => Plugin.Active?.FinishPurchase(true));
            return;
        }
        finally { inventory.ChangeMoneyWithoutReceipt(originalMoney - inventory.Money); }
        receipts[receipt.Id] = receipt;
        store.Set("restock-receipts", receipts);
        if (job.Index < job.Targets.Count) { Scheduler.NextFrame(Step); return; }
        int toCharge = Math.Max(0, receipt.CostHundredths - job.Credit);
        receipt.ChargeEddies = (toCharge + 99) / 100;
        receipt.RefundEddies = receipt.BudgetEddies - receipt.ChargeEddies;
        receipt.CreditHundredths = job.Credit + receipt.ChargeEddies * 100 - receipt.CostHundredths;
        receipt.Status = "committed";
        receipts[receipt.Id] = receipt;
        store.Set("restock-receipts", receipts);
        store.Set("restock-credit-hundredths", receipt.CreditHundredths);
        store.Set("last-restock-day", receipt.Day);
        foreach (var venue in job.Targets.Select(t => t.Venue).Where(v => v != null).DistinctBy(v => v!.Pointer))
            venue!.UpdateSupplyStates();
        if (!Singleton<SerializationManager>.Instance.Save(job.Save, false))
        {
            receipt.Status = "needs_recovery";
            receipts[receipt.Id] = receipt;
            store.Set("restock-receipts", receipts);
            receipt.Shortages.Add("purchase checkpoint failed; do not repeat with a new ID");
        }
        Singleton<ShoppingListManager>.Instance.UpdateListExternal();
        active = null;
        Plugin.Active?.FinishPurchase(receipt.Status != "committed");
    }
}
