using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;

namespace StreetChem;

// The shop copies real products; it does not grant synthetic Cyberpunk-only inventory.
internal static class GuestShop
{
    static readonly string[] Families={"Marijuana","Methamphetamine","Cocaine","Mushroom"};
    public static List<ProductItemInstance> Products()
    {
        var offers=new List<ProductItemInstance>();
        var slots=PlayerInventory.Instance.hotbarSlots;
        foreach(string family in Families) {
            ProductItemInstance? selected=null;
            foreach(var slot in slots) {
                var product=slot.ItemInstance?.TryCast<ProductItemInstance>();
                if(product!=null && slot.Quantity>0 && Matches(GuestConsumables.Family(product),family)){selected=product;break;}
            }
            if(selected==null)foreach(var item in Registry.Instance.GetAllItems()) {
                var definition=item.TryCast<ProductDefinition>();
                if(definition==null || definition.DrugTypes.Count==0 || !Matches(definition.DrugTypes[0].DrugType.ToString(),family))continue;
                // Prefer vanilla base products to a random custom blend.
                if(definition.ID is not ("ogkush" or "meth" or "cocaine" or "shroom" or "shrooms"))continue;
                selected=definition.GetDefaultInstance(1).TryCast<ProductItemInstance>();
                if(selected!=null){selected.SetPackaging(GuestGrowing.Bag());break;}
            }
            if(selected!=null)offers.Add(selected);
        }
        return offers;
    }
    static bool Matches(string actual,string family)=>actual==family || (family=="Mushroom" && (actual.Contains("Shroom")||actual.Contains("Mushroom")));
    public static int Price(ProductItemInstance product)=>Math.Clamp((int)Math.Ceiling(product.GetMonetaryValue()/Math.Max(1,product.Quantity)*12.5),1,1000000);
    public static object[] Catalog()=>Products().Select(product=> {
        GuestConsumables.ExportIcon(product);
        return (object)new {key=GuestConsumables.Key(product),id=product.ID,name=product.Name,quality=product.Quality.ToString(),packaging=product.PackagingID,amount=product.Amount,family=GuestConsumables.Family(product),price=Price(product),product=true};
    }).ToArray();
}
