using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using UnityEngine;

namespace StreetChem;

// Read only: inspect real user-installed prefab geometry without spawning network objects.
internal static class GuestEquipment
{
    public static object Catalog()
    {
        if (LoadManager.Instance?.IsGameLoaded != true)
            return new { ok=false, error="guest_not_loaded" };
        var definitions=Registry.Instance.GetAllItems();
        var items=new List<object>();
        foreach(var definition in definitions)
        {
            var buildable=definition.TryCast<BuildableItemDefinition>();
            if(buildable==null || buildable.BuiltItem==null) continue;
            var prefab=buildable.BuiltItem;
            var renderers=prefab.GetComponentsInChildren<Renderer>(true);
            var meshes=prefab.GetComponentsInChildren<MeshFilter>(true);
            int vertices=0;
            foreach(var mesh in meshes) if(mesh.sharedMesh!=null) vertices+=mesh.sharedMesh.vertexCount;
            var collider=prefab.BoundingCollider;
            items.Add(new {
                id=definition.ID, name=definition.Name,
                prefab=prefab.name, type=prefab.GetIl2CppType().FullName,
                renderers=renderers.Length, meshes=meshes.Length, vertices,
                bounds=collider==null?null:new {
                    x=collider.size.x,y=collider.size.y,z=collider.size.z,
                    center=new {x=collider.center.x,y=collider.center.y,z=collider.center.z}
                }
            });
        }
        return new {ok=true, items};
    }
}
