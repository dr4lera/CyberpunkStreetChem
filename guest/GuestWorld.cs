using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Building;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Tiles;
using UnityEngine;

namespace StreetChem;

internal sealed class GuestWorld
{
    readonly Dictionary<string,int> starters=new();
    readonly Dictionary<string,string> failures=new();
    public object State(JsonElement request){
        RequireSession();var id=request.GetProperty("id").GetString()??"";var guid=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(id)).AsSpan(0,16)).ToString();
        if(failures.TryGetValue(guid,out var error))return new {ok=false,status="error",id,guid,detail=error};
        if(Find(guid)==null)return new {ok=false,status="error",id,guid,detail="equipment_missing"};
        return new {ok=true,status=starters.ContainsKey(guid)?"preparing":"placed",id,guid};
    }
    public void Tick(){
        foreach(var entry in starters.ToArray()){
            if(Time.frameCount<entry.Value)continue;
            var pot=Find(entry.Key)?.TryCast<Il2CppScheduleOne.ObjectScripts.Pot>();
            if(pot==null||!pot.Initialized)continue;
            try {GuestGrowing.SupplyStarter(pot);starters.Remove(entry.Key);GuestSave.Changed();}
            catch(Exception e){MelonLoader.MelonLogger.Warning("Starter setup failed: "+e.Message);failures[entry.Key]=e.Message;starters.Remove(entry.Key);pot.Destroy();}
        }
    }
    public static object Layout()
    {
        RequireSession();
        var properties=new List<object>();
        foreach(var property in Property.OwnedProperties) {
            var grids=new List<object>();
            foreach(var grid in property.Grids) {
                int free=0;foreach(var pair in grid.CoordinateTilePairs)if(pair.tile!=null&&pair.tile.CanBeBuiltOn())free++;
                grids.Add(new {guid=grid.GUID.ToString(),width=grid.Width,height=grid.Height,tiles=grid.CoordinateTilePairs.Count,free});
            }
            var items=new List<object>();
            foreach(var item in property.BuildableItems)if(item!=null)items.Add(new {guid=item.GUID.ToString(),id=item.ItemInstance?.ID,name=item.name,initialized=item.Initialized});
            properties.Add(new {id=property.PropertyCode,name=property.PropertyName,grids,items});
        }
        return new {ok=true,properties};
    }
    public static void RequireSession()
    {
        if(LoadManager.Instance?.IsGameLoaded!=true||Player.Local==null||Player.PlayerList.Count!=1)
            throw new InvalidOperationException("loaded_single_player_required");
    }
    public static BuildableItem? Find(string guid)
    {
        foreach(var property in Property.OwnedProperties)
            foreach(var item in property.BuildableItems)
                if(item!=null && !item.IsDestroyed && item.GUID.ToString()==guid)return item;
        return null;
    }
    public object Create(JsonElement request)
    {
        RequireSession();
        var id=request.GetProperty("id").GetString()??"";
        if(id.Length<8||id.Length>96)throw new InvalidDataException("invalid_placement_id");
        var guid=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(id)).AsSpan(0,16)).ToString();
        if(request.TryGetProperty("guid",out var requestedGuid)){
            if(!Guid.TryParse(requestedGuid.GetString(),out var parsed))throw new InvalidDataException("invalid_replacement_guid");
            guid=parsed.ToString();
        }
        var existing=Find(guid);
        if(existing!=null){
            if(request.TryGetProperty("replace",out var replace)&&replace.GetBoolean())existing.Destroy();
            else return new {ok=true,id,status=starters.ContainsKey(guid)?"preparing":"placed",guid};
        }
        var equipment=request.GetProperty("equipment").GetString()??"";
        var definition=Registry.GetItem(equipment)?.TryCast<BuildableItemDefinition>();
        var prefab=definition?.BuiltItem?.TryCast<GridItem>();
        if(prefab==null)throw new InvalidOperationException("equipment_requires_surface_or_special_grid");
        foreach(var property in Property.OwnedProperties) foreach(var grid in property.Grids) {
            // The obsolete tutorial RV remains in OwnedProperties but is not saved by this game version.
            if(property.PropertyCode.Equals("rv",StringComparison.OrdinalIgnoreCase))continue;
            if(request.TryGetProperty("property",out var requestedProperty)&&property.PropertyCode!=requestedProperty.GetString())continue;
            foreach(var pair in grid.CoordinateTilePairs) {
                bool valid=true;
                // Validate every actual footprint tile using the game's placement rules.
                foreach(var footprint in prefab.CoordinateFootprintTilePairs) {
                    var coordinate=new Coordinate(pair.coord.x+footprint.coord.x,pair.coord.y+footprint.coord.y);
                    if(!grid.IsTileValidAtCoordinate(coordinate,footprint.footprintTile,prefab)){valid=false;break;}
                }
                if(!valid || prefab.CoordinateFootprintTilePairs.Count==0)continue;
                var instance=definition!.GetDefaultInstance(1);
                var built=BuildManager.Instance.CreateGridItem(instance,grid,new Vector2(pair.coord.x,pair.coord.y),0,guid,null);
                if(built==null)throw new InvalidOperationException("native_placement_failed");
                property.SetContentCulled(false);
                try {
                    if(request.TryGetProperty("starter",out var starter)&&starter.GetBoolean()){
                        var pot=built.TryCast<Il2CppScheduleOne.ObjectScripts.Pot>()??throw new InvalidOperationException("starter_requires_pot");
                        starters[guid]=Time.frameCount+2;
                    }
                }catch{built.Destroy();throw;}
                return new {ok=true,id,status=starters.ContainsKey(guid)?"preparing":"placed",guid,equipment,property=property.PropertyCode};
            }
        }
        throw new InvalidOperationException("no_free_space_in_guest_owned_property");
    }
    public object Use(JsonElement request)
    {
        RequireSession();
        var item=Find(request.GetProperty("guid").GetString()??"")??throw new InvalidOperationException("equipment_missing");
        var interactables=item.GetComponentsInChildren<Il2CppScheduleOne.Interaction.InteractableObject>(true);
        if(interactables.Length==0)throw new InvalidOperationException("equipment_has_no_interaction");
        // Dispatch the real station event rather than reproducing its production rules.
        interactables[0].StartInteract();
        return new {ok=true,status="using",guid=item.GUID.ToString()};
    }
}
