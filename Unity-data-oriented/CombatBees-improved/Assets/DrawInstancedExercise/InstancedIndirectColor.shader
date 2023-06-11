Shader "Custom/InstancedIndirectColor" {
    SubShader {
        Tags { "RenderType" = "Opaque" }

        Pass {
            Tags {"LightMode"="ForwardBase"}
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "AutoLight.cginc"
            
            struct appdata_t {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
            };

            struct v2f {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
            }; 

            struct MeshProperties {
                float4x4 mat;
            };

            StructuredBuffer<MeshProperties> _Properties;
            float4 _Color;

            v2f vert(appdata_full i, uint instanceID: SV_InstanceID) {
                v2f o;

                /*float4x4 mat = float4x4(
                    1, 0, 0, 0,
                    0, 1, 0, 0,
                    0, 0, 1, 0,
                    0, 0, 0, 1
                );
                float4 pos = mul(mat, i.vertex);*/
                float4 pos = mul(_Properties[instanceID].mat, i.vertex);
                o.vertex = UnityObjectToClipPos(pos);
                o.color = _Color;

                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target {
                return i.color;
                //return fixed4(0, 0, 0, 1);
            }
            
            ENDCG
        }
    }
}