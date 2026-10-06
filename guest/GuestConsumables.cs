using System.Security.Cryptography;
using System.Text;
using Il2CppScheduleOne.Product;
using UnityEngine;

namespace StreetChem;

// Stable item records distinguish mixed products, quality and packaging across restarts.
internal static class GuestConsumables
{
    static readonly HashSet<string> exported=new();
    public static void ExportIcon(ProductItemInstance product)
    {
        string key=Key(product);if(exported.Contains(key))return;
        string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StreetChem","icon-source");
        string path=Path.Combine(folder,key+".png");
        if(File.Exists(path)){exported.Add(key);return;}
        var sprite=product.Icon;if(sprite==null||sprite.texture==null)return;
        var target=RenderTexture.GetTemporary(256,256,0,RenderTextureFormat.ARGB32);
        var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        try {
            var rect=sprite.rect;var source=sprite.texture;
            Graphics.Blit(source,target,new Vector2(rect.width/source.width,rect.height/source.height),new Vector2(rect.x/source.width,rect.y/source.height));
            RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
            Directory.CreateDirectory(folder);File.WriteAllBytes(path,ImageConversion.EncodeToPNG(pixels).ToArray());exported.Add(key);
        }catch(Exception e){MelonLoader.MelonLogger.Warning("Product icon export: "+e.Message);}
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(pixels);}
    }
    public static string Key(ProductItemInstance product) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(product.ID+"|"+product.Quality+"|"+product.PackagingID))).ToLowerInvariant()[..24];

    public static string Family(ProductItemInstance product)
    {
        var definition=product.Definition.TryCast<ProductDefinition>();
        if(definition?.DrugTypes?.Count>0)return definition.DrugTypes[0].DrugType.ToString();
        return "Drug";
    }
}
