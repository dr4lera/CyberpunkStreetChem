// Street Chem solo v0.2.0: V is Using now. ReShade Standard effects provide this include.
#include "ReShade.fxh"
texture2D SCLabFrame : STREETCHEM_LAB;
sampler2D SCLabSampler { Texture = SCLabFrame; };
uniform bool SCLabVisible = false;
uniform bool SCWorldVisual = true;
uniform float SCLabAspect = 1.7777778;
uniform float SCSaturation = 1.0;
uniform float SCVignette = 0.0;
uniform float SCTime < source = "timer"; >;
float3 SCDrugColor(float3 color, float2 uv) {
    float3 unchanged = color;
    float intensity = max(0.0, SCSaturation-1.0);
    float saturation = SCSaturation>1.0 ? 1.0+intensity*2.75 : SCSaturation;
    float grey = dot(color, float3(0.2126,0.7152,0.0722));
    color = lerp(grey.xxx, color, saturation);
    if(intensity>0.0) {
        float pulse = sin(SCTime*0.0018)*0.025;
        color = (color-0.5)*(1.0+intensity*(0.40+pulse))+0.5;
        color *= 1.0+intensity*0.10;
        color += float3(0.06,0.02,-0.03)*intensity;
    }
    float2 edge = uv * 2.0 - 1.0;
    float3 result = max(0.0, color * (1.0 - SCVignette * 1.7 * dot(edge,edge)));
    return abs(SCSaturation-1.0)<0.001 && SCVignette<0.001 ? unchanged : result;
}
float4 SCLabPS(float4 position : SV_Position,float2 uv : TEXCOORD) : SV_Target {
    if(!SCLabVisible) return float4(SCDrugColor(tex2D(ReShade::BackBuffer,uv).rgb,uv),1);
    // Isolated visual gate: camera synchronized; host depth, light and collision remain pending.
    if(SCWorldVisual) {
        float4 guest=tex2D(SCLabSampler,uv);
        return float4(SCDrugColor(lerp(tex2D(ReShade::BackBuffer,uv).rgb,guest.rgb,guest.a),uv),1);
    }
    // Preserve guest aspect; this is a live lab-view layer, not spatial world fusion.
    float aspect = float(BUFFER_WIDTH)/BUFFER_HEIGHT;
    float2 scale = SCLabAspect>aspect ? float2(1.0,aspect/SCLabAspect) : float2(SCLabAspect/aspect,1.0);
    float2 guestUV=(uv-0.5)/scale+0.5;
    if(any(guestUV<0.0)||any(guestUV>1.0)) return float4(0.015,0.025,0.03,1);
    return float4(tex2D(SCLabSampler,guestUV).rgb,1);
}
technique StreetChemLab {pass {VertexShader=PostProcessVS;PixelShader=SCLabPS;}}
