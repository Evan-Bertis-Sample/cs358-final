using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace LostInLeaves.WatercolorRendering
{
    public class WatercolorRenderFeature : ScriptableRendererFeature
    {
        // [SerializeField] private bool _activeInSceneView = false;

        private bool _initialized = false;
        private List<ScriptableRenderPass> _renderPasses = new List<ScriptableRenderPass>();

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            #if UNITY_EDITOR
            if (renderingData.cameraData.cameraType != CameraType.Game)
            {
                return;
            }
            #endif

            if (!_initialized)
            {
                _initialized = true;
            }

            foreach (var pass in _renderPasses)
            {
                renderer.EnqueuePass(pass);
            }
        }

        public override void Create()
        {
            Debug.Log("WatercolorRenderFeature::Create");
            _renderPasses = new List<ScriptableRenderPass>
            {
                new ShadowNoisePass(),
                new AnisotropicKuwaharaPass(),
                new SobelOutlinePass(),
                new PaperTexturePass(),
            };
        }
    }
}