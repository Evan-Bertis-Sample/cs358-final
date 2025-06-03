using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostInLeaves.WatercolorRendering
{
    public class ShadowNoisePass : ScriptableRenderPass, IDisposable
    {
        internal class ShadowNoiseComputer
        {
            public class ShaderTargets
            {
                public RenderTargetIdentifier ColorTarget;
                public RenderTargetIdentifier DistortionTextureTarget;
                public RenderTargetIdentifier OutputTarget;
            }

            public Vector2Int Resolution;

            private ComputeShader _shadowNoiseComputeShader;
            private ShaderTargets _shaderTargets;

            private int _mainKernel = -1;
            private Vector3Int _groups = new Vector3Int(8, 8, 1);
            private static string _shaderPath = "Watercolor Rendering/ShadowNoise";

            // Shader uniforms
            private const string COLOR_TEXTURE = "InputTexture";
            private const string DISTORTION_TEXTURE = "DistortionTexture";
            private const string OUTPUT_TEXTURE = "OutputTexture";

            public bool CanCompute => _shadowNoiseComputeShader != null;

            public ShadowNoiseComputer(CommandBuffer cmd, RenderTextureDescriptor descriptor)
            {
                InitializeShader(cmd, descriptor);
#if UNITY_EDITOR
                ComputeShaderPostprocessor.AddImportHandler(_shadowNoiseComputeShader, shader =>
                {
                    Debug.Log("PaperTexturePassComputer::ComputeShaderPostprocessor: Shader reloaded");
                    Free();
                    InitializeShader(cmd, descriptor);
                    SetTargets(cmd, _shaderTargets);
                });
#endif
            }

            private void InitializeShader(CommandBuffer cmd, RenderTextureDescriptor descriptor)
            {
                _shadowNoiseComputeShader = Resources.Load<ComputeShader>(_shaderPath);
                if (_shadowNoiseComputeShader == null)
                {
                    Debug.LogWarning($"ShadowNoiseComputer::InitializeShader: Could not load shader at path {_shaderPath}");
                    return;
                }

                _mainKernel = _shadowNoiseComputeShader.FindKernel("CSMain");
                Resolution = new Vector2Int(descriptor.width, descriptor.height);
            }

            public void SetTargets(CommandBuffer cmd, ShaderTargets targets)
            {
                _shaderTargets = targets;

                cmd.SetComputeTextureParam(_shadowNoiseComputeShader, _mainKernel, COLOR_TEXTURE, targets.ColorTarget);
                cmd.SetComputeTextureParam(_shadowNoiseComputeShader, _mainKernel, DISTORTION_TEXTURE, targets.DistortionTextureTarget);
                cmd.SetComputeTextureParam(_shadowNoiseComputeShader, _mainKernel, OUTPUT_TEXTURE, targets.OutputTarget);
            }

            public RenderTargetIdentifier Compute(CommandBuffer cmd, RenderTextureDescriptor descriptor)
            {
                Vector3Int groupCount = new Vector3Int(
                    Mathf.CeilToInt(Resolution.x / (float)_groups.x),
                    Mathf.CeilToInt(Resolution.y / (float)_groups.y),
                    _groups.z
                );

                cmd.DispatchCompute(_shadowNoiseComputeShader, _mainKernel, groupCount.x, groupCount.y, groupCount.z);
                return _shaderTargets.OutputTarget;
            }

            public void Free()
            {
                
            }
        }

        private ShadowNoiseComponent _settings;
        private ShadowNoiseComputer _computer;
        private ShadowNoiseComputer.ShaderTargets _inputTargets;

        private RenderTargetIdentifier _outputIdentifier;
        private int _OUTPUT_ID = Shader.PropertyToID("_ShadowNoisePass_Output");

        public ShadowNoisePass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (_settings == null)
            {
                _settings = VolumeManager.instance.stack.GetComponent<ShadowNoiseComponent>();
            }
            if (_settings == null || !_settings.IsActive()) return;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.enableRandomWrite = true;

            if (_computer == null)
            {
                _computer = new ShadowNoiseComputer(cmd, descriptor);
                _outputIdentifier = new RenderTargetIdentifier(_OUTPUT_ID);
            }

            _computer.Resolution = new Vector2Int(descriptor.width, descriptor.height);

            cmd.GetTemporaryRT(_OUTPUT_ID, descriptor.width, descriptor.height, descriptor.depthBufferBits, FilterMode.Bilinear,
                                descriptor.colorFormat, RenderTextureReadWrite.Default, 1, true, descriptor.memoryless);


            _inputTargets = new ShadowNoiseComputer.ShaderTargets
            {
                ColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle,
                DistortionTextureTarget = _settings.distortionTexture.value,
                OutputTarget = _outputIdentifier
            };
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_settings == null || !_settings.IsActive()) return;
            if (_computer == null || !_computer.CanCompute)
            {
                Debug.LogWarning("ShadowNoisePass::Execute: ShadowNoiseComputer cannot compute. Check for valid shader in resources!");
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("ShadowNoisePass");
            using (new ProfilingScope(cmd, new ProfilingSampler("ShadowNoisePass::Compute")))
            {
                _inputTargets.ColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                _computer.SetTargets(cmd, _inputTargets);
                
                // Compute the shadow noise effect
                RenderTargetIdentifier output = _computer.Compute(cmd, renderingData.cameraData.cameraTargetDescriptor);
                ConfigureTarget(output);
                // Blit the result to the camera target
                Blit(cmd, output, renderingData.cameraData.renderer.cameraColorTargetHandle);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_OUTPUT_ID);
        }

        public void Dispose()
        {
            _computer?.Free();
        }
    }
}
