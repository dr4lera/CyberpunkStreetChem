// Street Chem solo release v0.1.0. ReShade Standard effects provide this include.
#include "ReShade.fxh"
texture2D SCLabFrame : STREETCHEM_LAB;
sampler2D SCLabSampler { Texture = SCLabFrame; };
uniform bool SCLabVisible = false;
uniform bool SCWorldVisual = true;
uniform float SCLabAspect = 1.7777778;
float4 SCLabPS(float4 position : SV_Position,float2 uv : TEXCOORD) : SV_Target {
    if(!SCLabVisible) return tex2D(ReShade::BackBuffer,uv);
    // Isolated visual gate: camera synchronized; host depth, light and collision remain pending.
    if(SCWorldVisual) {
        float4 guest=tex2D(SCLabSampler,uv);
        return float4(lerp(tex2D(ReShade::BackBuffer,uv).rgb,guest.rgb,guest.a),1);
    }
    // Preserve guest aspect; this is a live lab-view layer, not spatial world fusion.
    float aspect = float(BUFFER_WIDTH)/BUFFER_HEIGHT;
    float2 scale = SCLabAspect>aspect ? float2(1.0,aspect/SCLabAspect) : float2(SCLabAspect/aspect,1.0);
    float2 guestUV=(uv-0.5)/scale+0.5;
    if(any(guestUV<0.0)||any(guestUV>1.0)) return float4(0.015,0.025,0.03,1);
    return float4(tex2D(SCLabSampler,guestUV).rgb,1);
}
technique StreetChemLab {pass {VertexShader=PostProcessVS;PixelShader=SCLabPS;}}
