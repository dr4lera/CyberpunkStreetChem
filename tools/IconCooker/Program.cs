// SPDX-License-Identifier: GPL-3.0-only
// Copyright (c) 2026 zrock. Standalone converter using the user's WolvenKit installation.
using System.Reflection;
using System.Runtime.Loader;
using WolvenKit.RED4.Archive.CR2W;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.Types;

if(args.Length!=2)throw new ArgumentException("IconCooker <WolvenKit console folder> <cooked resources folder>");
AssemblyLoadContext.Default.Resolving+=(_,name)=>{
    string path=Path.Combine(args[0],name.Name+".dll");return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path)):null;
};
Cook(args[1]);

static void Cook(string folder)
{
    foreach(string texture in Directory.EnumerateFiles(folder,"*.xbm",SearchOption.AllDirectories)) {
        string key=Path.GetFileNameWithoutExtension(texture);
        if(key.Length!=24||key.Any(c=>!Uri.IsHexDigit(c)))continue;
        string depot="streetchem/icons/"+key+".xbm";
        var part=new inkTextureAtlasMapper {
            PartName="icon",ClippingRectInPixels=new Rect {Left=0,Top=0,Right=256,Bottom=256},
            ClippingRectInUVCoords=new RectF {Left=0,Top=0,Right=1,Bottom=1}
        };
        var parts=new CArray<inkTextureAtlasMapper>{part};
        var atlas=new inkTextureAtlas {IsSingleTextureMode=true,Texture=new CResourceAsyncReference<CBitmapTexture>(depot),Parts=parts};
        if(atlas.Slots.Count>0){atlas.Slots[0]=new inkTextureSlot {Texture=atlas.Texture,Parts=parts};}
        var file=new CR2WFile {RootChunk=atlas};
        using var stream=File.Create(Path.ChangeExtension(texture,".inkatlas"));
        using var writer=new CR2WWriter(stream);writer.WriteFile(file);
        Console.WriteLine("Cooked atlas: "+key);
    }
}
