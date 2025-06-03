using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostInLeaves.WatercolorRendering
{
    [VolumeComponentMenuForRenderPipeline("Lost In Leaves/Watercolor/Anisotropic Kuwahara", typeof(UniversalRenderPipeline))]
    public class AnisotropicKuwaharaComponent : VolumeComponent, IPostProcessComponent
    {
        [SerializeField, Range(2, 20)] public IntParameter KernelSizeParam = new IntParameter(2);
        [SerializeField, Range(1.0f, 18.0f)] public FloatParameter SharpnessParam = new FloatParameter(8.0f);
        [SerializeField, Range(1.0f, 100.0f)] public FloatParameter HardnessParam = new FloatParameter(8.0f);
        [SerializeField, Range(0.01f, 2.0f)] public FloatParameter AlphaParam = new FloatParameter(1.0f);
        [SerializeField, Range(0.01f, 2.0f)] public FloatParameter ZeroCrossingParam = new FloatParameter(0.58f);
        [SerializeField] public BoolParameter UseZetaParam = new BoolParameter(false);
        [SerializeField, Range(0.01f, 3.0f)] public FloatParameter ZetaParam = new FloatParameter(1.0f);
        [SerializeField, Range(1, 4)] public IntParameter PassesParam = new IntParameter(1);

        public int KernelSize => KernelSizeParam.value;
        public float Sharpness => SharpnessParam.value;
        public float Hardness => HardnessParam.value;
        public float Alpha => AlphaParam.value;
        public float ZeroCrossing => ZeroCrossingParam.value;
        public bool UseZeta => UseZetaParam.value;
        public float Zeta => ZetaParam.value;
        public int Passes => PassesParam.value;

        // IPostProcessComponent
        public bool IsActive() => active;
        public bool IsTileCompatible() => false;
    }
}
