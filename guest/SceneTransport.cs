using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace StreetChem;
internal sealed class SceneTransport : IDisposable
{
    MemoryMappedFile? poseMap, frameMap;
    MemoryMappedViewAccessor? pose, frame;
    Mutex? frameLock;
    byte[]? bytes;
    byte[]? sourceBytes;
    long sequence;
    public int WorldKey {get;private set;}
    [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
    public bool Camera(out Vector3 position,out Vector3 forward,out Vector3 up,out float fov,out float aspect)
    {
        position=forward=up=default;fov=aspect=0;
        try {
            if(pose==null){poseMap=MemoryMappedFile.OpenExisting("Local\\StreetChem.Pose.v1",MemoryMappedFileRights.Read);pose=poseMap.CreateViewAccessor(0,80,MemoryMappedFileAccess.Read);}
            uint before=pose.ReadUInt32(8);
            if((before&1)!=0 || pose.ReadUInt32(0)!=0x5343504F || pose.ReadUInt32(4)!=1) return false;
            ulong now=GetTickCount64(), tick=pose.ReadUInt64(16);
            if(now<tick || now-tick>250) return false;
            position=Vector(pose,24);forward=Vector(pose,36);up=Vector(pose,48);
            WorldKey=pose.ReadInt32(12);
            fov=pose.ReadSingle(60);aspect=pose.ReadSingle(64);
            return before==pose.ReadUInt32(8) && fov>10 && fov<170 && aspect>0.5f && aspect<5;
        } catch(FileNotFoundException){return false;}
    }
    static Vector3 Vector(MemoryMappedViewAccessor data,int offset)=>new(data.ReadSingle(offset),data.ReadSingle(offset+4),data.ReadSingle(offset+8));
    public void Publish(Il2CppStructArray<Color32> colors,int width,int height)
    {
        if(frame==null){frameMap=MemoryMappedFile.CreateOrOpen("Local\\StreetChem.World.v1",48+3840L*2160*4,MemoryMappedFileAccess.ReadWrite);frame=frameMap.CreateViewAccessor();frameLock=new Mutex(false,"Local\\StreetChem.World.v1.Lock");}
        bool acquired=false;
        try {try {acquired=frameLock!.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
            if(!acquired)return;
            int length=width*height*4;
            if(bytes?.Length!=length) bytes=new byte[length];
            if(colors.Length!=width*height)throw new InvalidDataException("frame_size_mismatch");
            if(sourceBytes?.Length!=length)sourceBytes=new byte[length];
            // IL2CPP x64 array: object header, bounds pointer, length, then packed Color32 data.
            // One native copy replaces millions of interop element reads per frame.
            Marshal.Copy(IntPtr.Add(colors.Pointer,4*IntPtr.Size),sourceBytes,0,length);
            int stride=width*4;
            for(int y=0;y<height;y++)Buffer.BlockCopy(sourceBytes,(height-1-y)*stride,bytes,y*stride,stride);
            frame.WriteArray(48,bytes,0,length);
            frame.Write(0,0x57575054u);frame.Write(4,1u);frame.Write(8,(uint)width);frame.Write(12,(uint)height);
            frame.Write(16,(uint)(width*4));frame.Write(20,(uint)length);frame.Write(24,(uint)Environment.ProcessId);frame.Write(28,0u);
            frame.Write(32,++sequence);frame.Write(40,GetTickCount64());
        } finally {if(acquired)frameLock!.ReleaseMutex();}
    }
    public void Dispose(){pose?.Dispose();poseMap?.Dispose();frame?.Dispose();frameMap?.Dispose();frameLock?.Dispose();pose=null;poseMap=null;frame=null;frameMap=null;frameLock=null;}
}
