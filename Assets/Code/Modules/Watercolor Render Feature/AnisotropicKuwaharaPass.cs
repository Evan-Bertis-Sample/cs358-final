using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostInLeaves.WatercolorRendering
{
    public class AnisotropicKuwaharaPass : ScriptableRenderPass
    {
        private Material _kuwaharaMaterial;
        private Shader _kuwaharaShader;
        private AnisotropicKuwaharaComponent _config;

        private RenderTargetIdentifier _inputTexture;
        private List<RenderTargetIdentifier> _passTextures = new List<RenderTargetIdentifier>();

        private int PassTextureID(int pass) => Shader.PropertyToID($"_PassTexture{pass}");

        public AnisotropicKuwaharaPass()
        {
            _kuwaharaShader = Shader.Find("Hidden/AnisotropicKuwahara");
            _kuwaharaMaterial = CoreUtils.CreateEngineMaterial(_kuwaharaShader);
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        private void PassParameters()
        {
            _kuwaharaMaterial.SetInt("_KernelSize", _config.KernelSize);
            _kuwaharaMaterial.SetInt("_N", 8);
            _kuwaharaMaterial.SetFloat("_Q", _config.Sharpness);
            _kuwaharaMaterial.SetFloat("_Hardness", _config.Hardness);
            _kuwaharaMaterial.SetFloat("_Alpha", _config.Alpha);
            _kuwaharaMaterial.SetFloat("_ZeroCrossing", _config.ZeroCrossing);
            _kuwaharaMaterial.SetFloat("_Zeta", _config.UseZeta ? _config.Zeta : 2.0f / 2.0f / (_config.KernelSize / 2.0f));
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            base.OnCameraSetup(cmd, ref renderingData);
            _inputTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // grab our settings
            if (_config == null)
            {
                _config = VolumeManager.instance.stack.GetComponent<AnisotropicKuwaharaComponent>();
            }
            if (_config == null) return;
            if (_config.IsActive() == false) return;

            CommandBuffer cmd = CommandBufferPool.Get("AnisotropicKuwahara");
            // create our temporary textures
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.enableRandomWrite = true;
            PassParameters();

            using (new ProfilingScope(cmd, new ProfilingSampler("AnisotropicKuwaharaPass::Compute")))
            {
                cmd.SetGlobalTexture("_TFM", Shader.GetGlobalTexture("_CameraNormalsTexture"));
                _inputTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
                ConfigureTarget(_inputTexture);
                // create the needed textures for the passes
                _passTextures.Clear();
                for (int i = 0; i < _config.Passes; i++)
                {
                    RenderTargetIdentifier passTexture = new RenderTargetIdentifier(PassTextureID(i));
                    cmd.GetTemporaryRT(PassTextureID(i), descriptor.width, descriptor.height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBFloat);
                    _passTextures.Add(passTexture);
                }

                Blit(cmd, _inputTexture, _passTextures[0], _kuwaharaMaterial, 3);

                // do our passes
                for (int i = 1; i < _config.Passes; i++)
                {
                    Debug.Log("AnisotropicKuwaharaPass::Execute: Pass " + i);
                    Blit(cmd, _passTextures[i - 1], _passTextures[i], _kuwaharaMaterial, 3);
                }

                // blit our final pass to the camera target
                Blit(cmd, _passTextures[_config.Passes - 1], renderingData.cameraData.renderer.cameraColorTargetHandle);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            base.OnCameraCleanup(cmd);

            int pass = 0;
            foreach (var passTexture in _passTextures)
            {
                cmd.ReleaseTemporaryRT(PassTextureID(pass));
                pass++;
            }
        }
    }
}
