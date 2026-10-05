using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.PlayerScripts;
using System.Text.Json;

namespace StreetChem;
internal static class GuestRunners
{
    public static int CashEddies(float cash)=>float.IsFinite(cash)?(int)Math.Clamp(Math.Floor((double)cash*10),0,int.MaxValue):0;
    public static Dealer Find(string id) {
        GuestWorld.RequireSession();
        foreach(var dealer in Dealer.AllPlayerDealers)if(dealer!=null && dealer.IsRecruited && dealer.ID==id)return dealer;
        throw new InvalidOperationException("recruited_runner_missing");
    }
    public static object[] Catalog() {
        var rows=new List<object>();
        if(Il2CppScheduleOne.Persistence.LoadManager.Instance?.IsGameLoaded!=true)return rows.ToArray();
        foreach(var dealer in Dealer.AllPlayerDealers)if(dealer!=null && dealer.IsRecruited) {
            int quantity=0;foreach(var slot in dealer.ItemSlots)if(slot.ItemInstance?.TryCast<ProductItemInstance>()!=null)quantity+=slot.Quantity;
            rows.Add(new {id=dealer.ID,name=dealer.FullName,quantity,cut=dealer.DealerData.SalesCutPercentage,cash=CashEddies(dealer.Cash)});
        }
        return rows.ToArray();
    }
    public static object Stock(JsonElement request) {
        var dealer=Find(request.GetProperty("dealer").GetString()??"");
        int index=request.GetProperty("slot").GetInt32();int qty=request.GetProperty("quantity").GetInt32();
        var inventory=PlayerInventory.Instance;
        if(index<0||index>=inventory.hotbarSlots.Count||qty<1||qty>10)throw new InvalidOperationException("invalid_runner_stock_amount");
        var source=inventory.hotbarSlots[index];
        var product=source.ItemInstance?.TryCast<ProductItemInstance>();
        if(product==null||source.IsRemovalLocked||source.Quantity<qty)throw new InvalidOperationException("stock_unavailable");
        int capacity=0;foreach(var slot in dealer.ItemSlots)capacity+=slot.GetCapacityForItem(product);
        if(capacity<qty)throw new InvalidOperationException("runner_inventory_full");
        var copy=product.GetCopy(qty);
        source.SetQuantity(source.Quantity-qty,true);dealer.AddItemToInventory(copy);GuestSave.Changed();
        return new {ok=true,status="stocked",dealer=dealer.ID,quantity=qty};
    }
}
