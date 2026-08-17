Shader "Murillo/Training Overlay Unlit"
{
    Properties
    {
        _Color ("Color", Color) = (0.05, 1, 0.92, 1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            Color [_Color]
        }
    }
}
