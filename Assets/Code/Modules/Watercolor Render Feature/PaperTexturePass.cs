using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostInLeaves.WatercolorRendering
{
    public class PaperTexturePass : ScriptableRenderPass, IDisposable
    {
        internal class PaperTextureComputer
        {
            public class ShaderTargets
            {
                public RenderTargetIdentifier ColorTarget;
                public RenderTargetIdentifier PaperTextureTarget;
                public RenderTargetIdentifier MaskTarget;
                public RenderTargetIdentifier OutputTarget;
            }

            public Vector2Int Resolution;

            private ComputeShader _paperTextureComputeShader;
            private ShaderTargets _shaderTargets;

            private int _mainKernel = -1;
            private Vector3Int _groups = new Vector3Int(8, 8, 1);
            private static string _shaderPath = "Watercolor Rendering/PaperTexture";

            // Shader uniforms
            private const string COLOR_TEXTURE = "ColorTexture";
            private const string PAPER_TEXTURE = "PaperTexture";
            private const string MASK_TEXTURE = "MaskTexture";
            private const string OUTPUT_TEXTURE = "OutputTexture";

            public bool CanCompute => _paperTextureComputeShader != null;

            public PaperTextureComputer(CommandBuffer cmd, RenderTextureDescriptor descriptor)
            {
                InitializeShader(cmd, descriptor);

#if UNITY_EDITOR
                ComputeShaderPostprocessor.AddImportHandler(_paperTextureComputeShader, shader =>
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
                _paperTextureComputeShader = Resources.Load<ComputeShader>(_shaderPath);
                if (_paperTextureComputeShader == null)
                {
                    Debug.LogWarning($"PaperTextureComputer::InitializeShader: Could not load shader at path {_shaderPath}");
                    return;
                }

                _mainKernel = _paperTextureComputeShader.FindKernel("CSMain");
                Resolution = new Vector2Int(descriptor.width, descriptor.height);
            }

            public void SetTargets(CommandBuffer cmd, ShaderTargets targets)
            {
                _shaderTargets = targets;

                cmd.SetComputeTextureParam(_paperTextureComputeShader, _mainKernel, COLOR_TEXTURE, targets.ColorTarget);
                cmd.SetComputeTextureParam(_paperTextureComputeShader, _mainKernel, PAPER_TEXTURE, targets.PaperTextureTarget);
                cmd.SetComputeTextureParam(_paperTextureComputeShader, _mainKernel, MASK_TEXTURE, targets.MaskTarget);
                cmd.SetComputeTextureParam(_paperTextureComputeShader, _mainKernel, OUTPUT_TEXTURE, targets.OutputTarget);
            }

            public RenderTargetIdentifier Compute(CommandBuffer cmd, RenderTextureDescriptor descriptor)
            {
                Vector3Int groupCount = new Vector3Int(
                    Mathf.CeilToInt(Resolution.x / (float)_groups.x),
                    Mathf.CeilToInt(Resolution.y / (float)_groups.y),
                    _groups.z
                );

                cmd.DispatchCompute(_paperTextureComputeShader, _mainKernel, groupCount.x, groupCount.y, groupCount.z);
                return _shaderTargets.OutputTarget;
            }

            public void Free()
            {
                // Clean-up resources or logics if necessary
            }
        }

        private PaperTextureComponent _settings;
        private PaperTextureComputer _computer;
        private PaperTextureComputer.ShaderTargets _inputTargets;

        private RenderTargetIdentifier _outputIdentifier;
        private int _OUTPUT_ID => Shader.PropertyToID("_PaperTexturePass_Output");

        public PaperTexturePass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Fetch the settings from the volume
            if (_settings == null)
            {
                _settings = VolumeManager.instance.stack.GetComponent<PaperTextureComponent>();
            }
            if (_settings == null || !_settings.IsActive()) return;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.enableRandomWrite = true;

            if (_computer == null)
            {
                _computer = new PaperTextureComputer(cmd, descriptor);
                _outputIdentifier = new RenderTargetIdentifier(_OUTPUT_ID);
            }

            _computer.Resolution = new Vector2Int(descriptor.width, descriptor.height);

            cmd.GetTemporaryRT(_OUTPUT_ID, descriptor.width, descriptor.height, descriptor.depthBufferBits, FilterMode.Bilinear,
                                descriptor.colorFormat, RenderTextureReadWrite.Default, 1, true, descriptor.memoryless);

            _inputTargets = new PaperTextureComputer.ShaderTargets
            {
                ColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle,
                PaperTextureTarget = _settings.PaperTexture.value ?? new RenderTargetIdentifier(BuiltinRenderTextureType.DepthNormals), // Assuming PaperTexture is directly accessible
                MaskTarget = _settings.Mask.value ?? new RenderTargetIdentifier(BuiltinRenderTextureType.Depth), // Assuming Mask is directly accessible
                OutputTarget = _outputIdentifier
            };
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Fetch the settings from the volume
            if (_settings == null)
            {
                _settings = VolumeManager.instance.stack.GetComponent<PaperTextureComponent>();
            }
            if (_settings == null || !_settings.IsActive()) return;

            // Ensure the computer is ready to compute
            if (_computer == null || !_computer.CanCompute)
            {
                Debug.LogWarning("PaperTexturePass::Execute: PaperTextureComputer cannot compute. Check for valid shader in resources!");
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("PaperTexturePass");
            using (new ProfilingScope(cmd, new ProfilingSampler("PaperTexturePass::Compute")))
            {
                _inputTargets.ColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

                _computer.SetTargets(cmd, _inputTargets);

                // Compute the paper texture effect
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
            base.OnCameraCleanup(cmd);
            cmd.ReleaseTemporaryRT(_OUTPUT_ID);
        }

        public void Dispose()
        {
            _computer?.Free();
        }
    }
}
