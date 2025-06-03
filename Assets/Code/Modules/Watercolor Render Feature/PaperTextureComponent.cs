using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostInLeaves.WatercolorRendering
{
    [VolumeComponentMenuForRenderPipeline("Lost In Leaves/Watercolor/Paper Texture", typeof(UniversalRenderPipeline))]
    public class PaperTextureComponent : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter Active = new BoolParameter(true, true);
        public TextureParameter PaperTexture = new TextureParameter(null);
        public TextureParameter Mask = new TextureParameter(null);

        public bool IsActive() => Active.value && PaperTexture.value != null;
        public bool IsTileCompatible() => false;
    }

}
