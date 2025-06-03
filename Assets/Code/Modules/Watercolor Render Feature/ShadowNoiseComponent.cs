using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostInLeaves.WatercolorRendering
{
    [VolumeComponentMenuForRenderPipeline("Custom/Shadow Noise Pass", typeof(UniversalRenderPipeline))]
    public class ShadowNoiseComponent : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter isActive = new BoolParameter(false);
        public TextureParameter distortionTexture = new TextureParameter(null);

        public bool IsActive() => isActive.value && this.distortionTexture.value != null;
        public bool IsTileCompatible() => false;
    }
}