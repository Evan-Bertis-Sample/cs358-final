using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostInLeaves.WatercolorRendering
{
    [VolumeComponentMenuForRenderPipeline("Lost In Leaves/Watercolor/Sobel Outlines", typeof(UniversalRenderPipeline))]
    public class SobelOutlineComponent : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter Active = new BoolParameter(true, true);
        public ColorParameter OutlineColor = new ColorParameter(Color.black);
        // IPostProcessComponent
        public bool IsActive() => Active.value;
        public bool IsTileCompatible() => false;
    }
}
