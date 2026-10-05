using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using UnityEngine;

namespace StreetChem;

// Visual-only copies: no guest production, save or network components are duplicated.
// This is the isolated-render gate, not the finished placement/production implementation.
internal sealed class EquipmentVisual
{
    GameObject? root;
    Camera? camera;
    RenderTexture? target;
    Texture2D? pixels;
    readonly SceneTransport transport=new();
    bool world;
    float nextFrame;
    static Vector3 Map(Vector3 p)=>new(p.x,p.z,p.y);
    public object StartWorld(string id,Vector3 position,float yaw)
    {
        var definition=Registry.GetItem(id)?.TryCast<BuildableItemDefinition>();
        if(definition?.BuiltItem==null) return new {ok=false,error="unknown_equipment"};
        Clear();
        root=new GameObject("StreetChem.WorldVisualGate");
        root.transform.position=Map(position);
        root.transform.rotation=Quaternion.Euler(0,-yaw,0);
        Copy(definition.BuiltItem.transform,root.transform);
        camera=new GameObject("StreetChem.WorldCamera").AddComponent<Camera>();
        camera.enabled=false;camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0,0,0,0);
        camera.nearClipPlane=0.05f;camera.farClipPlane=500;
        target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        pixels=new Texture2D(960,540,TextureFormat.RGBA32,false);
        world=true;
        return new {ok=true,status="visual_only",id};
    }
    public void Tick()
    {
        if(!world || camera==null || pixels==null || Time.unscaledTime<nextFrame)return;
        nextFrame=Time.unscaledTime+0.05f;
        if(!transport.Camera(out var position,out var forward,out var up,out var fov,out var aspect))return;
        camera.transform.position=Map(position);
        camera.transform.rotation=Quaternion.LookRotation(Map(forward),Map(up));
        camera.fieldOfView=fov;camera.aspect=aspect;
        camera.Render();
        var previous=RenderTexture.active;
        try {RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();transport.Publish(pixels.GetPixels32(),960,540);}
        finally {RenderTexture.active=previous;}
    }
    public object Preview(string id)
    {
        var definition=Registry.GetItem(id)?.TryCast<BuildableItemDefinition>();
        if(definition?.BuiltItem==null) return new {ok=false,error="unknown_equipment"};
        Clear();
        root=new GameObject("StreetChem.VisualGate");
        root.transform.position=new Vector3(0,10000,0);
        Copy(definition.BuiltItem.transform,root.transform);
        var bounds=new Bounds(root.transform.position,new Vector3(1,1,1));
        foreach(var renderer in root.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
        var cameraObject=new GameObject("StreetChem.IsolatedCamera");
        camera=cameraObject.AddComponent<Camera>();
        camera.enabled=false;
        camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(0,0,0,0);
        camera.fieldOfView=50;
        camera.nearClipPlane=0.05f;
        camera.farClipPlane=100;
        camera.transform.position=bounds.center+new Vector3(2,1.5f,-3)*Mathf.Max(1,bounds.size.magnitude*0.5f);
        camera.transform.LookAt(bounds.center);
        target=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32);
        target.Create();
        camera.targetTexture=target;
        pixels=new Texture2D(960,540,TextureFormat.RGBA32,false);
        camera.Render();
        var previous=RenderTexture.active;
        try {
            RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,960,540),0,0);
            pixels.Apply();
            var rgba=pixels.GetPixels32();
            int covered=0;
            foreach(var color in rgba) if(color.a>0) covered++;
            // Coverage proves actual render output; it does not prove correct host depth/lighting.
            return new {ok=true,id,renderers=root.GetComponentsInChildren<Renderer>().Length,coveredPixels=covered,totalPixels=rgba.Length,
                bounds=new {x=bounds.size.x,y=bounds.size.y,z=bounds.size.z},
                pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name ?? "built-in"};
        } finally {RenderTexture.active=previous; Clear();}
    }
    static void Copy(Transform source,Transform parent)
    {
        var node=new GameObject(source.name);
        node.layer=30;
        node.transform.SetParent(parent,false);
        node.transform.localPosition=source.localPosition;
        node.transform.localRotation=source.localRotation;
        node.transform.localScale=source.localScale;
        var filter=source.GetComponent<MeshFilter>();
        var renderer=source.GetComponent<MeshRenderer>();
        if(filter!=null && renderer!=null && filter.sharedMesh!=null && renderer.enabled)
        {
            node.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            var clone=node.AddComponent<MeshRenderer>();
            clone.sharedMaterials=renderer.sharedMaterials;
        }
        // Skinned/animated production visuals need a live source mapping in the next gate.
        for(int i=0;i<source.childCount;i++) Copy(source.GetChild(i),node.transform);
        node.SetActive(source.gameObject.activeSelf);
    }
    void Clear()
    {
        world=false;
        if(camera!=null) UnityEngine.Object.Destroy(camera.gameObject);
        if(root!=null) UnityEngine.Object.Destroy(root);
        if(target!=null){target.Release();UnityEngine.Object.Destroy(target);}
        if(pixels!=null) UnityEngine.Object.Destroy(pixels);
        camera=null;root=null;target=null;pixels=null;
    }
}
