using System.Text.Json;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Product.Packaging;

namespace StreetChem;
internal static class GuestGrowing
{
    public static SeedDefinition Seed()
    {
        foreach(var definition in Registry.Instance.GetAllItems()) {
            var seed=definition.TryCast<SeedDefinition>();
            if(seed?.PlantPrefab!=null && seed.IsUnlocked &&
                (seed.Name.Contains("OG Kush",StringComparison.OrdinalIgnoreCase) ||
                 seed.ID.Equals("ogkushseed",StringComparison.OrdinalIgnoreCase)))return seed;
        }
        throw new InvalidOperationException("no_unlocked_og_kush_cannabis_seed");
    }
    public static void SupplyStarter(Pot pot)
    {
        var seed=Seed();
        var instance=seed.GetDefaultInstance(1);
        if(!PlayerInventory.Instance.CanItemFitInInventory(instance,1))throw new InvalidOperationException("make_space_for_seed_in_guest_inventory");
        if(pot.AllowedSoils.Length==0)throw new InvalidOperationException("pot_has_no_allowed_soil");
        var soil=pot.AllowedSoils[0];
        pot.SetSoil(soil);pot.SetSoilAmount(pot.SoilCapacity);pot.SetRemainingSoilUses(soil.Uses);pot.SyncSoilData();
        pot.SetMoistureAmount(pot.MoistureCapacity);pot.SyncMoistureData();
        PlayerInventory.Instance.AddItemToInventory(instance);
    }
    public static object Status(string guid)
    {
        GuestWorld.RequireSession();
        var pot=GuestWorld.Find(guid)?.TryCast<Pot>()??throw new InvalidOperationException("pot_missing");
        float speed=0;
        var exposure=pot.GetAverageLightExposure(out speed);
        return new {ok=true,status="pot",guid,seed=pot.Plant?.SeedDefinition?.ID,
            growth=pot.Plant?.NormalizedGrowthProgress??0,ready=pot.Plant?.IsFullyGrown==true,
            soil=pot.NormalizedSoilAmount,water=pot.NormalizedMoistureAmount,light=exposure,soilCapacity=pot.SoilCapacity,waterCapacity=pot.MoistureCapacity,initialized=pot.Initialized,
            growing=pot.Plant!=null,yield=pot.Plant?.BaseYieldQuantity};
    }
    public static object Act(JsonElement request)
    {
        GuestWorld.RequireSession();
        var guid=request.GetProperty("guid").GetString()??"";
        var pot=GuestWorld.Find(guid)?.TryCast<Pot>()??throw new InvalidOperationException("pot_missing");
        var action=request.GetProperty("action").GetString();
        string reason="";
        switch(action){
            case "supply":
                if(pot.Plant!=null)throw new InvalidOperationException("harvest_before_refilling");
                SupplyStarter(pot);
                GuestSave.Changed();
                return new {ok=true,status="supplied",guid};
            case "plant":
                if(pot.Plant!=null)return new {ok=true,status="already_planted",guid};
                if(!pot.CanAcceptSeed(out reason))throw new InvalidOperationException(reason);
                var seed=Seed();
                if(PlayerInventory.Instance.GetAmountOfItem(seed.ID)==0)throw new InvalidOperationException("seed_required");
                pot.PlantSeed_Server(seed.ID,0);
                if(pot.Plant==null)throw new InvalidOperationException("native_plant_creation_failed");
                PlayerInventory.Instance.RemoveAmountOfItem(seed.ID,1);
                break;
            case "water":
                pot.SetMoistureAmount(pot.MoistureCapacity);pot.SyncMoistureData();
                break;
            case "harvest":
                if(!pot.IsReadyForHarvest(out reason))throw new InvalidOperationException(reason);
                var plant=pot.Plant;
                var harvestables=plant._harvestables.ToArray();
                var active=new List<int>();int quantity=0;
                for(int i=0;i<harvestables.Length;i++)if(harvestables[i]!=null && plant.IsHarvestableActive(i)){active.Add(i);quantity+=harvestables[i].ProductQuantity;}
                if(active.Count==0 || quantity<=0)throw new InvalidOperationException("no_native_harvestables_available");
                var product=plant.GetHarvestedProduct(quantity);
                var bagged=product.TryCast<ProductItemInstance>()??throw new InvalidOperationException("harvest_is_not_native_product");
                bagged.SetPackaging(Bag());
                if(!PlayerInventory.Instance.CanItemFitInInventory(product,product.Quantity))throw new InvalidOperationException("make_space_for_harvest");
                // Use the plant's actual remaining harvestables and native completion callbacks;
                // the mouse-task Harvest method assumes a live Schedule I player task.
                PlayerInventory.Instance.AddItemToInventory(product);
                foreach(var index in active)pot.SetHarvestableActive_Server(index,false);
                if(pot.Plant!=null && pot.Plant.ActiveHarvestables.Count==0)pot.OnPlantFullyHarvested();
                var packed=PackInventory();
                GuestSave.Changed();
                return new {ok=true,status="harvested",guid,collected=quantity,packed=packed+quantity};
            default:throw new InvalidOperationException("unknown_growing_action");
        }
        GuestSave.Changed();
        return new {ok=true,status=action,guid};
    }
    public static int PackInventory()
    {
        GuestWorld.RequireSession();
        var bag=Bag();
        int packed=0;
        foreach(var slot in PlayerInventory.Instance.hotbarSlots) {
            var product=slot.ItemInstance?.TryCast<ProductItemInstance>();
            if(product==null || slot.IsRemovalLocked || product.AppliedPackaging!=null)continue;
            product.SetPackaging(bag);packed+=slot.Quantity;
        }
        if(packed>0)GuestSave.Changed();
        return packed;
    }
    internal static PackagingDefinition Bag()
    {
        foreach(var definition in Registry.Instance.GetAllItems()) {
            var packaging=definition.TryCast<PackagingDefinition>();
            if(packaging!=null && packaging.Quantity==1 && packaging.ID.Contains("baggie",StringComparison.OrdinalIgnoreCase))return packaging;
        }
        throw new InvalidOperationException("native_baggie_definition_missing");
    }
}
