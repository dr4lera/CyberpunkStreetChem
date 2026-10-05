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
    sealed record Reservation(string Id,string Session,string Dealer,int Slot,string Product,string Packaging,string Quality,int Quantity,int Before,int Price,ItemInstance? Copy,long Expires);
    static IReadOnlyList<ItemSlot> Slots(string dealer)=>dealer.Length==0?PlayerInventory.Instance.hotbarSlots.ToArray().Select(slot=>(ItemSlot)slot).ToArray():GuestRunners.Find(dealer).ItemSlots.ToArray();
    static object Quote(Reservation sale)=>new {ok=true,id=sale.Id,status="reserved",quantity=sale.Quantity,product=sale.Product,quality=sale.Quality,packaging=sale.Packaging,price=sale.Price,dealer=sale.Dealer};
    Reservation? pending;
    readonly Dictionary<string,object> receipts=new();
    string journalSession="",journalPath="";
    object? committing;
    bool awaitingCashDebit;
    float cashTarget;
    public void Tick() {
        if(awaitingCashDebit && pending!=null) {
            if(GuestRunners.Find(pending.Dealer).Cash!=cashTarget)return;
            awaitingCashDebit=false;SaveManager.Instance.Save();return;
        }
        if(committing!=null && pending!=null && LoadManager.Instance?.LoadedGameFolderPath==pending.Session && !SaveManager.Instance.IsSaving && !SaveManager.SaveError) {
            try {receipts[pending.Id]=committing;WriteJournal();committing=null;pending=null;}
            catch(Exception e){receipts.Remove(pending!.Id);MelonLoader.MelonLogger.Warning("Sale receipt write failed: "+e.Message);}
        }
        if(committing==null && pending!=null && Environment.TickCount64>pending.Expires) Abort(pending.Id);
    }
    void LoadJournal(string session) {
        if(journalSession==session)return;
        if(pending!=null)throw new InvalidOperationException("finish_pending_sale_before_switching_save");
        var key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session)));
        journalPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StreetChem","sales",key+".json");
        receipts.Clear();
        if(File.Exists(journalPath)) {
            using var doc=JsonDocument.Parse(File.ReadAllText(journalPath));
            if(doc.RootElement.GetProperty("session").GetString()!=session)throw new InvalidOperationException("sale_journal_session_mismatch");
            foreach(var row in doc.RootElement.GetProperty("receipts").EnumerateObject())receipts[row.Name]=row.Value.Clone();
        }
        journalSession=session;
    }
    void WriteJournal() {
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        var temp=journalPath+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
            JsonSerializer.Serialize(stream,new {schema=1,session=journalSession,receipts});stream.Flush(true);
        }
        File.Move(temp,journalPath,true);
    }
    public object Handle(JsonElement request)
    {
        GuestWorld.RequireSession();LoadJournal(LoadManager.Instance.LoadedGameFolderPath);
        var op=request.GetProperty("op").GetString();
        var id=request.GetProperty("id").GetString()??"";
        if(id.Length<8||id.Length>96) throw new InvalidDataException("invalid_transaction_id");
        if(op=="reserve") return Reserve(request,id);
        if(op=="collect") return Collect(request,id);
        if(op=="abort") return Abort(id);
        if(op=="commit") return Commit(id);
        throw new InvalidDataException("unknown_sale_operation");
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
            pending=null;
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
        if(pending?.Id!=id) throw new InvalidOperationException("reservation_missing_or_expired");
        var sale=pending;
        if(SaveManager.Instance.IsSaving)return new {ok=true,id,status="waiting_for_save"};
        if(LoadManager.Instance?.LoadedGameFolderPath!=sale.Session) throw new InvalidOperationException("guest_session_changed");
        var slot=Slots(sale.Dealer)[sale.Slot];
        var product=slot.ItemInstance?.TryCast<ProductItemInstance>();
        if(product==null||product.ID!=sale.Product||product.PackagingID!=sale.Packaging||product.Quality.ToString()!=sale.Quality||slot.Quantity!=sale.Before) {
            Abort(id);
            throw new InvalidOperationException("reserved_stack_changed");
        }
        slot.SetIsRemovalLocked(false);
        slot.SetQuantity(sale.Before-sale.Quantity,true);
        if(slot.Quantity!=sale.Before-sale.Quantity) throw new InvalidOperationException("native_stock_debit_failed");
        if(sale.Dealer.Length>0)GuestRunners.Find(sale.Dealer).ChangeCash(sale.Price/10f);
        object receipt=new {ok=true,id,status="committed",session=sale.Session,dealer=sale.Dealer,price=sale.Price,product=sale.Product,quantity=sale.Quantity,before=sale.Before,after=slot.Quantity};
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
