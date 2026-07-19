Shader "Oasis/FaceMissingOrientationProperties"
{
    Properties
    {
        [MainTexture] _OasisArtworkTex ("Artwork", 2D) = "white" {}
        _OasisMaskTex ("Mask", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Cull Back
        }
    }
}
