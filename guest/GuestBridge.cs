using MelonLoader;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using UnityEngine;

[assembly: MelonInfo(typeof(StreetChem.GuestBridge), "Street Chem Guest Bridge", "0.1.0", "zrock / Codex")]
[assembly: MelonGame("TVGS", "Schedule I")]
namespace StreetChem;

public sealed class GuestBridge : MelonMod
{
    readonly CancellationTokenSource stop = new();
    readonly ConcurrentQueue<Pending> requests = new();
    readonly RemoteInput input = new();
    readonly GuestSales sales = new();
    readonly EquipmentVisual equipmentVisual = new();
    readonly GuestWorld world = new();
    readonly LiveEquipmentVisual liveVisual = new();
    sealed record Pending(JsonElement Request, TaskCompletionSource<string> Reply);
    static readonly string[] relevant = {"PlayerScripts.Player", "PlayerScripts.PlayerInventory", "PlayerScripts.PlayerMovement", "PlayerScripts.PlayerCamera", "Product.ProductManager", "Product.ProductDefinition", "Product.ProductItemInstance", "ItemFramework.ItemSlot", "ItemFramework.ItemInstance", "Money.MoneyManager", "Loading.LoadManager", "GameInput"};

    public override void OnInitializeMelon()
    {
        _ = Task.Run(Server);
        MelonLogger.Msg("StreetChem pipe started. Solo bridge ready. Load your paired save in both games. Controls: Ctrl+H in Cyberpunk.");
    }
    public override void OnUpdate()
    {
        input.PollSharedMemory();
        input.Tick();
        sales.Tick();
        world.Tick();
        GuestClock.Tick();
        GuestSave.Tick();
        equipmentVisual.Tick();
        liveVisual.Tick();
        for(int i=0; i<8 && requests.TryDequeue(out var request); i++)
        {
            if(request.Reply.Task.IsCompleted) continue;
            try {
                var op=request.Request.GetProperty("op").GetString();
                object result=op switch { "clock" => GuestClock.Status(), "wake" => GuestClock.Wake(), "runners" => new {ok=true,runners=GuestRunners.Catalog()}, "runner_stock" => GuestRunners.Stock(request.Request), "pack" => new {ok=true,status="packed",quantity=GuestGrowing.PackInventory()}, "adopt" => liveVisual.Adopt(), "save" => SaveGuest(), "save_status" => new {ok=true,saving=SaveManager.Instance.IsSaving,error=SaveManager.SaveError}, "api" => DescribeApi(), "placement_status" => world.State(request.Request), "bind" => liveVisual.Bind(request.Request), "grow" => GuestGrowing.Act(request.Request), "pot_status" => GuestGrowing.Status(request.Request.GetProperty("guid").GetString()??""), "layout" => GuestWorld.Layout(), "place" => world.Create(request.Request), "use" => world.Use(request.Request), "equipment" => GuestEquipment.Catalog(), "equipment_preview" => equipmentVisual.Preview(request.Request.GetProperty("equipment").GetString() ?? ""), "world_visual" => WorldVisual(request.Request), "observe" => Observe(), "load_last" => LoadLast(), "reserve" or "commit" or "abort" or "collect" => sales.Handle(request.Request), _ => new {ok=false,error="unknown_operation"} };
                request.Reply.TrySetResult(JsonSerializer.Serialize(result));
            } catch(Exception e) { request.Reply.TrySetResult(JsonSerializer.Serialize(new {ok=false,error=e.GetType().Name,detail=e.Message})); }
        }
    }
    static object SaveGuest() {GuestWorld.RequireSession();if(SaveManager.Instance.IsSaving)return new {ok=true,status="saving"};SaveManager.Instance.Save();return new {ok=true,status="saving"};}
    static object LoadLast() {if(LoadManager.Instance.IsGameLoaded||LoadManager.Instance.IsLoading)return new {ok=true,status="already_loaded_or_loading"};var info=LoadManager.LastPlayedGame;if(info==null)throw new InvalidOperationException("select_save_in_continue_menu");LoadManager.Instance.StartGame(info,false,true);return new {ok=true,status="loading"};}
    object WorldVisual(JsonElement request)
    {
        if(LoadManager.Instance?.IsGameLoaded!=true)return new {ok=false,error="guest_not_loaded"};
        var p=request.GetProperty("position");
        return equipmentVisual.StartWorld(request.GetProperty("equipment").GetString()??"",new Vector3(p.GetProperty("x").GetSingle(),p.GetProperty("y").GetSingle(),p.GetProperty("z").GetSingle()),request.GetProperty("yaw").GetSingle());
    }
    public override void OnDeinitializeMelon() {input.SetActive(false);liveVisual.Dispose();stop.Cancel();}
    async Task Server()
    {
        while(!stop.IsCancellationRequested) {
            try {
                using var pipe=new NamedPipeServerStream(BridgeConfig.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                using var reader=new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
                using var writer=new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush=true };
                while(pipe.IsConnected && !stop.IsCancellationRequested) {
                    // Bounded line reading prevents a local malformed sender from allocating indefinitely.
                    var bytes=new StringBuilder();
                    var ch=new char[1];
                    while(true) {
                        var count=await reader.ReadAsync(ch.AsMemory(),stop.Token);
                        if(count==0) throw new EndOfStreamException();
                        if(ch[0]=='\n') break;
                        if(bytes.Length>=BridgeConfig.MaxPacketBytes/2) throw new InvalidDataException("packet_too_large");
                        bytes.Append(ch[0]);
                    }
                    using var json=JsonDocument.Parse(bytes.ToString());
                    if(requests.Count>=BridgeConfig.MaxQueue) { await writer.WriteLineAsync("{\"ok\":false,\"error\":\"busy\"}"); continue; }
                    var reply=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                    requests.Enqueue(new Pending(json.RootElement.Clone(),reply));
                    var timeout=Task.Delay(TimeSpan.FromSeconds(BridgeConfig.TimeoutSeconds),stop.Token);
                    if(await Task.WhenAny(reply.Task,timeout)!=reply.Task) {
                        reply.TrySetCanceled();
                        await writer.WriteLineAsync("{\"ok\":false,\"error\":\"game_thread_timeout\"}");
                    } else await writer.WriteLineAsync(await reply.Task);
                }
            } catch(OperationCanceledException) { break; }
            catch(EndOfStreamException) { /* Normal one-request client close. */ }
            catch(IOException) { /* A polling client closed its pipe; next client may connect. */ }
            catch(Exception e) { MelonLogger.Warning("Bridge client disconnected: "+e.Message); }
        }
    }
    static Type[] GameTypes() => AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name=="Assembly-CSharp").SelectMany(a => a.GetTypes()).ToArray();
    static object DescribeApi() => new {ok=true, types=GameTypes().Where(t => relevant.Any(r => t.FullName?.EndsWith(r)==true)).Select(t => new {
        name=t.FullName,
        properties=t.GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Select(p=>new {name=p.Name,type=p.PropertyType.FullName,canWrite=p.CanWrite,isStatic=p.GetMethod?.IsStatic}),
        methods=t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(m=>!m.IsSpecialName).Select(m=>new {name=m.Name,returns=m.ReturnType.FullName,args=m.GetParameters().Select(p=>new {name=p.Name,type=p.ParameterType.FullName})})
    })};
    object Observe()
    {
        var load=LoadManager.Instance;
        var inventory=PlayerInventory.Instance;
        var items=new List<object>();
        if(inventory!=null) {
            for(int i=0;i<inventory.hotbarSlots.Count;i++) {
                var slot=inventory.hotbarSlots[i];
                var item=slot.ItemInstance;
                if(item==null) continue;
                var product=item.TryCast<ProductItemInstance>();
                items.Add(new {slot=i,id=item.ID,name=item.Name,quantity=slot.Quantity,product=product!=null,quality=product?.Quality.ToString(),packaging=product?.PackagingID,amount=product?.Amount,price=product?.GetMonetaryValue()});
            }
        }
        Application.runInBackground=true;
        return new {ok=true,loaded=load?.IsGameLoaded==true,session=load?.LoadedGameFolderPath,player=Player.Local?.PlayerName,position=Player.Local==null?null:new {x=Player.Local.transform.position.x,y=Player.Local.transform.position.y,z=Player.Local.transform.position.z},runners=GuestRunners.Catalog(),clock=GuestClock.Status(),inputActive=input.Active,motion=new {x=Il2CppScheduleOne.GameInput.MotionAxis.x,y=Il2CppScheduleOne.GameInput.MotionAxis.y},items};
    }
}
