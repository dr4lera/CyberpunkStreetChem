using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.ItemFramework;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace StreetChem;

// Stock is owned by the real game. Only an exact, locked inventory stack can be reserved.
internal sealed class GuestSales
{
    sealed record Reservation(string Id,string Session,string Dealer,int Slot,string Product,string Packaging,string Quality,int Quantity,int Before,int Price,ItemInstance? Copy,long Expires,bool Purchase=false);
    static IReadOnlyList<ItemSlot> Slots(string dealer)=>dealer.Length==0?PlayerInventory.Instance.hotbarSlots.ToArray().Select(slot=>(ItemSlot)slot).ToArray():GuestRunners.Find(dealer).ItemSlots.ToArray();
    static object Quote(Reservation sale)=>new {ok=true,id=sale.Id,status="reserved",quantity=sale.Quantity,product=sale.Product,quality=sale.Quality,packaging=sale.Packaging,price=sale.Price,dealer=sale.Dealer};
    Reservation? pending;
    readonly Dictionary<string,object> receipts=new();
    string journalSession="",journalPath="";
    object? committing;
    bool awaitingCashDebit;
    float cashTarget;
    bool purchaseApplying;
    public object Consume(JsonElement request)
    {
        GuestWorld.RequireSession();LoadJournal(LoadManager.Instance.LoadedGameFolderPath);
        string id=request.GetProperty("id").GetString()??"";
        if(id.Length<8||id.Length>96)throw new InvalidDataException("invalid_transaction_id");
        if(receipts.TryGetValue(id,out var receipt))return receipt;
        if(pending?.Id==id)return Commit(id);
        if(pending!=null)throw new InvalidOperationException("another_sale_is_pending");
        if(request.GetProperty("session").GetString()!=journalSession)throw new InvalidOperationException("guest_session_changed");
        var key=request.GetProperty("key").GetString();
        var slots=Slots("");
        for(int i=0;i<slots.Count;i++) {
            var slot=slots[i];var product=slot.ItemInstance?.TryCast<ProductItemInstance>();
            if(product==null||slot.Quantity<=0||slot.IsRemovalLocked||GuestConsumables.Key(product)!=key)continue;
            slot.SetIsRemovalLocked(true);
            pending=new Reservation(id,journalSession,"",i,product.ID,product.PackagingID,product.Quality.ToString(),1,slot.Quantity,0,product.GetCopy(1),Environment.TickCount64+30000);
            return Commit(id);
        }
        throw new InvalidOperationException("consumable_stock_unavailable");
    }
    public void Tick() {
        if(awaitingCashDebit && pending!=null) {
            if(GuestRunners.Find(pending.Dealer).Cash!=cashTarget)return;
            awaitingCashDebit=false;SaveManager.Instance.Save();return;
        }
        if(committing!=null && pending!=null && LoadManager.Instance?.LoadedGameFolderPath==pending.Session && !SaveManager.Instance.IsSaving && !SaveManager.SaveError) {
            try {receipts[pending.Id]=committing;WriteJournal();committing=null;pending=null;}
            catch(Exception e){receipts.Remove(pending!.Id);MelonLoader.MelonLogger.Warning("Sale receipt write failed: "+e.Message);}
        }
        if(committing==null && pending!=null && !pending.Purchase && Environment.TickCount64>pending.Expires) Abort(pending.Id);
    }
    void LoadJournal(string session) {
        if(journalSession==session)return;
        if(pending!=null)throw new InvalidOperationException("finish_pending_sale_before_switching_save");
        var sessionHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session)));
        journalPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StreetChem","sales",sessionHash+".json");
        receipts.Clear();
        if(File.Exists(journalPath)) {
            using var doc=JsonDocument.Parse(File.ReadAllText(journalPath));
            if(doc.RootElement.GetProperty("session").GetString()!=session)throw new InvalidOperationException("sale_journal_session_mismatch");
            foreach(var row in doc.RootElement.GetProperty("receipts").EnumerateObject())receipts[row.Name]=row.Value.Clone();
        }
        journalSession=session;
        if(File.Exists(journalPath)) {
            using var doc=JsonDocument.Parse(File.ReadAllText(journalPath));
            if(doc.RootElement.TryGetProperty("purchase",out var purchase) && purchase.ValueKind==JsonValueKind.Object && !receipts.ContainsKey(purchase.GetProperty("id").GetString()!)) {
                string id=purchase.GetProperty("id").GetString()!;
                string key=purchase.GetProperty("key").GetString()!;
                int index=purchase.GetProperty("slot").GetInt32(),before=purchase.GetProperty("before").GetInt32(),price=purchase.GetProperty("price").GetInt32();
                var slot=Slots("")[index];var item=slot.ItemInstance?.TryCast<ProductItemInstance>();
                purchaseApplying=purchase.GetProperty("applying").GetBoolean();
                if(purchaseApplying && item!=null && GuestConsumables.Key(item)==key && slot.Quantity==before+1) {
                    receipts[id]=new {ok=true,id,status="committed",session,price,purchase=true,quantity=1,before,after=slot.Quantity};WriteJournal();
                } else if(slot.Quantity==before && (before==0 || (item!=null && GuestConsumables.Key(item)==key))) {
                    var offer=GuestShop.Products().FirstOrDefault(p=>GuestConsumables.Key(p)==key)??throw new InvalidOperationException("purchase_recovery_product_unavailable");
                    slot.SetIsRemovalLocked(true);
                    pending=new Reservation(id,session,"",index,offer.ID,offer.PackagingID,offer.Quality.ToString(),-1,before,price,offer.GetCopy(1),Environment.TickCount64+120000,true);
                } else {journalSession="";throw new InvalidOperationException("purchase_recovery_stock_changed_restore_paired_saves");}
            }
        }
    }
    void WriteJournal() {
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        var temp=journalPath+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
            object? purchase=pending?.Purchase==true && !receipts.ContainsKey(pending.Id)?new {id=pending.Id,key=GuestConsumables.Key(pending.Copy!.TryCast<ProductItemInstance>()!),slot=pending.Slot,before=pending.Before,price=pending.Price,applying=purchaseApplying}:null;
            JsonSerializer.Serialize(stream,new {schema=1,session=journalSession,receipts,purchase});stream.Flush(true);
        }
        File.Move(temp,journalPath,true);
    }
    public object Handle(JsonElement request)
    {
        GuestWorld.RequireSession();LoadJournal(LoadManager.Instance.LoadedGameFolderPath);
        var op=request.GetProperty("op").GetString();
        var id=request.GetProperty("id").GetString()??"";
        if(id.Length<8||id.Length>96) throw new InvalidDataException("invalid_transaction_id");
        if(op=="buy_reserve") return BuyReserve(request,id);
        if(op=="buy_commit") return Commit(id);
        if(op=="buy_abort") return Abort(id);
        if(op=="reserve") return Reserve(request,id);
        if(op=="collect") return Collect(request,id);
        if(op=="abort") return Abort(id);
        if(op=="commit") return Commit(id);
        throw new InvalidDataException("unknown_sale_operation");
    }
    object BuyReserve(JsonElement request,string id)
    {
        if(request.GetProperty("session").GetString()!=journalSession)throw new InvalidOperationException("guest_session_changed");
        if(receipts.TryGetValue(id,out var done))return done;
        if(pending!=null) {
            if(pending.Id==id && pending.Purchase)return Quote(pending);
            throw new InvalidOperationException("another_sale_is_pending");
        }
        if(SaveManager.Instance.IsSaving)throw new InvalidOperationException("wait_for_native_save");
        string key=request.GetProperty("key").GetString()??"";
        var offer=GuestShop.Products().FirstOrDefault(p=>GuestConsumables.Key(p)==key)??throw new InvalidOperationException("shop_product_changed_refresh_menu");
        int price=GuestShop.Price(offer);
        if(Slots("").Where(s=>s.ItemInstance?.TryCast<ProductItemInstance>() is { } p && GuestConsumables.Key(p)==key).Sum(s=>(long)s.Quantity)>=99999999)throw new InvalidOperationException("consumable_mirror_capacity_reached");
        var slots=Slots("");int index=-1;
        for(int i=0;i<slots.Count;i++) {
            var product=slots[i].ItemInstance?.TryCast<ProductItemInstance>();
            if(product!=null && !slots[i].IsRemovalLocked && GuestConsumables.Key(product)==key && slots[i].Quantity<99999999){index=i;break;}
        }
        if(index<0)for(int i=0;i<slots.Count;i++)if(slots[i].Quantity==0 && !slots[i].IsRemovalLocked){index=i;break;}
        if(index<0)throw new InvalidOperationException("make_one_empty_hotbar_slot_in_schedule_i");
        var slot=slots[index];slot.SetIsRemovalLocked(true);
        pending=new Reservation(id,journalSession,"",index,offer.ID,offer.PackagingID,offer.Quality.ToString(),-1,slot.Quantity,price,offer.GetCopy(1),Environment.TickCount64+120000,true);
        purchaseApplying=false;
        try {WriteJournal();}catch {slot.SetIsRemovalLocked(false);pending=null;throw;}
        return Quote(pending);
    }
    object Reserve(JsonElement request,string id)
    {
        var load=LoadManager.Instance;
        if(load==null||!load.IsGameLoaded||Player.Local==null||Player.PlayerList.Count!=1) throw new InvalidOperationException("single_player_session_required");
        var session=request.GetProperty("session").GetString();
        if(session!=load.LoadedGameFolderPath) throw new InvalidOperationException("guest_session_changed");
        LoadJournal(session!);
        if(receipts.TryGetValue(id,out var done)) return done;
        if(pending!=null) {
            if(pending.Id==id) return Quote(pending);
            throw new InvalidOperationException("another_sale_is_pending");
        }
        string dealer=request.TryGetProperty("dealer",out var dealerField)?dealerField.GetString()??"":"";
        var slots=Slots(dealer);
        if(dealer.Length==0)GuestGrowing.PackInventory();
        int index=-1;
        if(dealer.Length==0)index=request.GetProperty("slot").GetInt32();
        else {
            for(int i=0;i<slots.Count;i++)if(slots[i].ItemInstance?.TryCast<ProductItemInstance>()!=null && !slots[i].IsRemovalLocked && slots[i].Quantity>0){index=i;break;}
        }
        var qty=dealer.Length==0?request.GetProperty("quantity").GetInt32():1;
        if(index<0||index>=slots.Count||qty<1||qty>100) throw new InvalidDataException("runner_has_no_stock");
        var slot=slots[index];
        var product=slot.ItemInstance?.TryCast<ProductItemInstance>();
        if(product==null||(dealer.Length==0 && product.ID!=request.GetProperty("product").GetString())||slot.Quantity<qty||slot.IsRemovalLocked) throw new InvalidOperationException("stock_unavailable");
        if(product.AppliedPackaging==null){product.SetPackaging(GuestGrowing.Bag());GuestSave.Changed();}
        var cut=dealer.Length==0?0:Math.Clamp(GuestRunners.Find(dealer).DealerData.SalesCutPercentage,0,1);
        int price=(int)Math.Floor(product.GetMonetaryValue()/slot.Quantity*10*(1-cut));
        var copy=product.GetCopy(qty);
        slot.SetIsRemovalLocked(true);
        pending=new Reservation(id,session!,dealer,index,product.ID,product.PackagingID,product.Quality.ToString(),qty,slot.Quantity,price,copy,Environment.TickCount64+30000);
        return Quote(pending);
    }
    object Abort(string id)
    {
        if(committing!=null && pending?.Id==id)return new {ok=true,id,status="committing"};
        if(pending?.Id==id) {
            if(LoadManager.Instance?.LoadedGameFolderPath==pending.Session) Slots(pending.Dealer)[pending.Slot].SetIsRemovalLocked(false);
            bool purchase=pending.Purchase;pending=null;
            if(purchase)WriteJournal();
        }
        return new {ok=true,id,status="aborted"};
    }
    object Commit(string id)
    {
        if(receipts.TryGetValue(id,out var done)) return done;
        if(committing!=null && pending?.Id==id) {
            if(SaveManager.SaveError && !SaveManager.Instance.IsSaving)SaveManager.Instance.Save();
            return new {ok=true,id,status="committing"};
        }
        if(pending?.Id!=id) {
            // A local recovery may finish an interrupted intent after this process loaded its journal.
            if(pending==null && File.Exists(journalPath)) {
                using var doc=JsonDocument.Parse(File.ReadAllText(journalPath));
                if(doc.RootElement.GetProperty("session").GetString()==journalSession && doc.RootElement.GetProperty("receipts").TryGetProperty(id,out var recovered)) {receipts[id]=recovered.Clone();return receipts[id];}
            }
            throw new InvalidOperationException("reservation_missing_or_expired");
        }
        var sale=pending;
        if(SaveManager.Instance.IsSaving)return new {ok=true,id,status="waiting_for_save"};
        if(LoadManager.Instance?.LoadedGameFolderPath!=sale.Session) throw new InvalidOperationException("guest_session_changed");
        var slot=Slots(sale.Dealer)[sale.Slot];
        var product=slot.ItemInstance?.TryCast<ProductItemInstance>();
        if(slot.Quantity!=sale.Before || (sale.Before>0 && (product==null||product.ID!=sale.Product||product.PackagingID!=sale.Packaging||product.Quality.ToString()!=sale.Quality))) {
            if(!sale.Purchase || !purchaseApplying)Abort(id);
            throw new InvalidOperationException("reserved_stack_changed");
        }
        if(sale.Purchase){purchaseApplying=true;WriteJournal();}
        slot.SetIsRemovalLocked(false);
        if(sale.Purchase && sale.Before==0)slot.SetStoredItem(sale.Copy,true);
        else if(sale.Purchase) {
            // Positive ItemSlot.SetQuantity routes through capacity-limited addition in this build.
            // The instance backing field preserves oversized native stacks; notify its normal listeners.
            product!._Quantity_k__BackingField=sale.Before-sale.Quantity;
            product.InvokeDataChange();
        } else slot.SetQuantity(sale.Before-sale.Quantity,true);
        if(slot.Quantity!=sale.Before-sale.Quantity) {
            if(sale.Purchase && product!=null){product._Quantity_k__BackingField=sale.Before;product.InvokeDataChange();}
            throw new InvalidOperationException(sale.Purchase?"native_stock_credit_failed":"native_stock_debit_failed");
        }
        if(sale.Dealer.Length>0)GuestRunners.Find(sale.Dealer).ChangeCash(sale.Price/10f);
        object receipt=new {ok=true,id,status="committed",session=sale.Session,dealer=sale.Dealer,price=sale.Price,product=sale.Product,quantity=Math.Abs(sale.Quantity),purchase=sale.Purchase,before=sale.Before,after=slot.Quantity};
        committing=receipt;
        SaveManager.Instance.Save();
        return new {ok=true,id,status="committing"};
    }
    object Collect(JsonElement request,string id)
    {
        if(receipts.TryGetValue(id,out var done))return done;
        if(committing!=null && pending?.Id==id) {
            if(SaveManager.SaveError && !SaveManager.Instance.IsSaving)SaveManager.Instance.Save();
            return new {ok=true,id,status="committing"};
        }
        if(pending!=null)throw new InvalidOperationException("another_sale_is_pending");
        if(SaveManager.Instance.IsSaving)return new {ok=true,id,status="waiting_for_save"};
        var dealer=GuestRunners.Find(request.GetProperty("dealer").GetString()??"");
        int limit=request.TryGetProperty("limit",out var limitField)?Math.Clamp(limitField.GetInt32(),0,1000000):1000000;
        float before=dealer.Cash;
        if(!float.IsFinite(before)||before<0)throw new InvalidOperationException("invalid_runner_cash");
        float target=Math.Max(0,before-limit/10f);
        double debit=((double)before-target)*10;
        if(debit>limit){target=MathF.BitIncrement(target);debit=((double)before-target)*10;}
        int price=(int)Math.Clamp(Math.Floor(debit),0,limit);
        if(price<=0)throw new InvalidOperationException("runner_has_no_cash");
        pending=new Reservation(id,journalSession,dealer.ID,-1,"cash","","",0,0,price,null,Environment.TickCount64+30000);
        cashTarget=target;awaitingCashDebit=true;
        committing=new {ok=true,id,status="committed",kind="collect",dealer=dealer.ID,price,before,after=target};
        dealer.SetCash(target);return new {ok=true,id,status="committing"};
    }
}
