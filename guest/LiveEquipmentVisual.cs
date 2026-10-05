using System.Text.Json;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Persistence;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace StreetChem;
internal sealed class LiveEquipmentVisual : IDisposable
{
    sealed class Mount {public string Guid="";public Vector3 Position;public float Yaw;public GameObject? Root;public readonly List<Mesh> Bakes=new();}
    readonly Dictionary<string,Mount> mounts=new();
    readonly SceneTransport transport=new();
    Camera? camera;RenderTexture? target;Texture2D? pixels;
    float nextFrame,nextRefresh;
    string session="";
    int hostWorld;
    string ManifestPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StreetChem","mounts",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(LoadManager.Instance.LoadedGameFolderPath)))+"-"+hostWorld+".json");
    // REDengine and Unity camera spaces have opposite handedness.
    static Vector3 Map(Vector3 p)=>new(p.x,p.z,p.y);
    public object Bind(JsonElement request)
    {
        GuestWorld.RequireSession();
        int requestedWorld=request.GetProperty("world").GetInt32();
        if(requestedWorld<=0)throw new InvalidDataException("host_world_required");
        if(hostWorld!=requestedWorld){ClearMounts();hostWorld=requestedWorld;}
        string guid=request.GetProperty("guid").GetString()??"";
        if(GuestWorld.Find(guid)==null)throw new InvalidOperationException("equipment_missing");
        if(!mounts.ContainsKey(guid)&&mounts.Count>=64)throw new InvalidOperationException("placement_limit_64");
        var p=request.GetProperty("position");
        var mount=mounts.TryGetValue(guid,out var old)?old:new Mount {Guid=guid};
        mount.Position=new Vector3(p.GetProperty("x").GetSingle(),p.GetProperty("y").GetSingle(),p.GetProperty("z").GetSingle());
        mount.Yaw=request.GetProperty("yaw").GetSingle();mounts[guid]=mount;
        Refresh(mount);EnsureCamera();
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            var manifest=JsonSerializer.Serialize(mounts.Values.Select(m=>new {guid=m.Guid,position=new {x=m.Position.x,y=m.Position.y,z=m.Position.z},yaw=m.Yaw}));
            File.WriteAllText(ManifestPath+".tmp",manifest);File.Move(ManifestPath+".tmp",ManifestPath,true);
        }
        return new {ok=true,status="bound",guid};
    }
    public object Adopt()
    {
        GuestWorld.RequireSession();
        var candidates=new List<BuildableItem>();
        foreach(var property in Il2CppScheduleOne.Property.Property.OwnedProperties)
            foreach(var item in property.BuildableItems)
                if(item!=null&&!item.IsDestroyed&&item.ItemInstance?.ID=="growtent"&&!mounts.ContainsKey(item.GUID.ToString()))candidates.Add(item);
        var chosen=candidates.FirstOrDefault(item=>item.TryCast<Il2CppScheduleOne.ObjectScripts.Pot>()?.Plant!=null)??candidates.FirstOrDefault();
        if(chosen==null)throw new InvalidOperationException("no_unlinked_grow_tent");
        return new {ok=true,status="placed",guid=chosen.GUID.ToString()};
    }
    void EnsureCamera()
    {
        if(camera!=null)return;
        camera=new GameObject("StreetChem.LiveEquipmentCamera").AddComponent<Camera>();
        camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(0,0,0,0);camera.nearClipPlane=0.05f;camera.farClipPlane=500;
        target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        pixels=new Texture2D(1280,720,TextureFormat.RGBA32,false);
    }
    void Refresh(Mount mount)
    {
        var item=GuestWorld.Find(mount.Guid);
        if(mount.Root!=null){mount.Root.SetActive(false);UnityEngine.Object.Destroy(mount.Root);mount.Root=null;}
        foreach(var bake in mount.Bakes)UnityEngine.Object.Destroy(bake);mount.Bakes.Clear();
        if(item==null)return;
        item.SetCulled(false);item.ParentProperty?.SetContentCulled(false);
        mount.Root=new GameObject("StreetChem.Live."+mount.Guid);
        mount.Root.transform.position=Map(mount.Position);mount.Root.transform.rotation=Quaternion.Euler(0,-mount.Yaw,0);
        Copy(item.transform,mount.Root.transform,mount.Bakes,true);
    }
    static void Copy(Transform source,Transform parent,List<Mesh> bakes,bool root=false)
    {
        // Placement grids and outline helpers are not equipment visuals.
        if(source.GetComponent<Il2CppScheduleOne.Tiles.FootprintTile>()!=null)return;
        var node=new GameObject(source.name);node.layer=30;node.transform.SetParent(parent,false);
        node.transform.localPosition=root?Vector3.zero:source.localPosition;
        node.transform.localRotation=root?Quaternion.identity:source.localRotation;
        node.transform.localScale=source.localScale;
        var filter=source.GetComponent<MeshFilter>();var renderer=source.GetComponent<MeshRenderer>();
        if(filter?.sharedMesh!=null&&renderer!=null&&renderer.enabled){
            bool valid=true;foreach(var material in renderer.sharedMaterials)if(material?.shader==null||!material.shader.isSupported||material.shader.name.Contains("InternalError")){valid=false;break;}
            if(valid){node.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;node.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;}
        }
        var skinned=source.GetComponent<SkinnedMeshRenderer>();
        if(skinned?.sharedMesh!=null&&skinned.enabled){
            // Bake the actual guest deformation rather than approximating growth stages.
            var mesh=new Mesh();skinned.BakeMesh(mesh);bakes.Add(mesh);
            node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>().sharedMaterials=skinned.sharedMaterials;
        }
        for(int i=0;i<source.childCount;i++)Copy(source.GetChild(i),node.transform,bakes);
        node.SetActive(root||source.gameObject.activeSelf);
    }
    public void Tick()
    {
        if(LoadManager.Instance?.IsGameLoaded==true&&session!=LoadManager.Instance.LoadedGameFolderPath){
            Dispose();session=LoadManager.Instance.LoadedGameFolderPath;
        }
        if(mounts.Count==0||camera==null||pixels==null||Time.unscaledTime<nextFrame)return;
        nextFrame=Time.unscaledTime+0.016f;
        if(!transport.Camera(out var position,out var forward,out var up,out var fov,out var aspect))return;
        if(transport.WorldKey!=hostWorld)return;
        if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+1;foreach(var mount in mounts.Values)Refresh(mount);}
        camera.transform.position=Map(position);camera.transform.rotation=Quaternion.LookRotation(Map(forward),Map(up));camera.fieldOfView=fov;camera.aspect=aspect;
        camera.Render();var previous=RenderTexture.active;
        try {RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();transport.Publish(pixels.GetPixels32(),1280,720);}
        finally{RenderTexture.active=previous;}
    }
    void ClearMounts(){foreach(var mount in mounts.Values){if(mount.Root!=null)UnityEngine.Object.Destroy(mount.Root);foreach(var bake in mount.Bakes)UnityEngine.Object.Destroy(bake);}mounts.Clear();}
    public void Dispose(){ClearMounts();if(camera!=null)UnityEngine.Object.Destroy(camera.gameObject);if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}if(pixels!=null)UnityEngine.Object.Destroy(pixels);camera=null;target=null;pixels=null;transport.Dispose();}
}
