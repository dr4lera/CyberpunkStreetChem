using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Nivalis;
using Nivalis.Economy;
using Nivalis.InventorySystem;
using NivalisModKit;

namespace NivalisNightCity;

// Match ModKit's vendor pipeline ABI. Generated IL2CPP trampolines corrupt
// by-ref ShopTradeRequest/BasicTemp structs because those contain object references.
internal static class NativePurchase
{
    private const string Method = "NativeMethodInfoPtr_BuyItem_Public_Void_byref_ShopTradeRequest_Single_byref_BasicTemp_0";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void BuyItemFn(IntPtr self, IntPtr request, float discount, IntPtr temp, IntPtr method);
    private static BuyItemFn? buy;
    private static IntPtr methodInfo;

    internal static void Buy(Vendor vendor, IntPtr customer, IntPtr container, ItemType item, int amount, int price)
    {
        if (amount <= 0 || price < 0) throw new ArgumentException("invalid native purchase");
        int requestSize = NivalisModKit.StructLayout.Size<ShopTradeRequest>();
        int tempSize = NivalisModKit.StructLayout.Size<ItemStack.BasicTemp>();
        if (requestSize != 40 || tempSize != 16) throw new NotSupportedException("unexpected native vendor layout");
        buy ??= Marshal.GetDelegateForFunctionPointer<BuyItemFn>(NativeHook.MethodPointer<Vendor>(Method));
        if (methodInfo == IntPtr.Zero) methodInfo = (IntPtr)(typeof(Vendor).GetField(Method,
            BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? throw new MissingFieldException(Method));
        var request = Marshal.AllocHGlobal(requestSize);
        var temp = Marshal.AllocHGlobal(tempSize);
        try
        {
            Marshal.Copy(new byte[requestSize], 0, request, requestSize);
            Marshal.Copy(new byte[tempSize], 0, temp, tempSize);
            Marshal.WriteIntPtr(request, NivalisModKit.StructLayout.FieldOffset<ShopTradeRequest>("customer"), customer);
            Marshal.WriteIntPtr(request, NivalisModKit.StructLayout.FieldOffset<ShopTradeRequest>("overrideDestinationContainer"), container);
            Marshal.WriteIntPtr(request, NivalisModKit.StructLayout.FieldOffset<ShopTradeRequest>("shop"), vendor.Pointer);
            Marshal.WriteIntPtr(request, NivalisModKit.StructLayout.FieldOffset<ShopTradeRequest>("itemType"), item.Pointer);
            Marshal.WriteInt32(request, NivalisModKit.StructLayout.FieldOffset<ShopTradeRequest>("freshness"), (int)FoodFreshness.Fresh);
            Marshal.WriteInt32(request, NivalisModKit.StructLayout.FieldOffset<ShopTradeRequest>("amount"), amount);
            Marshal.WriteIntPtr(temp, NivalisModKit.StructLayout.FieldOffset<ItemStack.BasicTemp>("Type"), item.Pointer);
            Marshal.WriteInt32(temp, NivalisModKit.StructLayout.FieldOffset<ItemStack.BasicTemp>("PricePerItem"), price);
            Marshal.WriteInt32(temp, NivalisModKit.StructLayout.FieldOffset<ItemStack.BasicTemp>("StackCount"), amount);
            buy(vendor.Pointer, request, 1f, temp, methodInfo);
        }
        finally { Marshal.FreeHGlobal(temp); Marshal.FreeHGlobal(request); }
    }
}
