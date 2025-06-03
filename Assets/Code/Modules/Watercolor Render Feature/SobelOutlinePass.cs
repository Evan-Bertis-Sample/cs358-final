using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LostInLeaves.WatercolorRendering
{
    public class SobelOutlinePass : ScriptableRenderPass, IDisposable
    {
        internal class SobelComputer
        {
            public class ShaderTargets
            {
                public RenderTargetIdentifier ColorTarget;
                public RenderTargetIdentifier DepthTarget;
                public RenderTargetIdentifier OutputTarget;
                public RenderTargetIdentifier NormalTarget;
            }

            public Vector2Int Resolution;

            private ComputeShader _sobelComputeShader;
            private ShaderTargets _sobelTargets;

            private int _mainKernel = -1;
            private Vector3Int _groups = new Vector3Int(8, 8, 1);
            private static string _shaderPath = "Watercolor Rendering/SobelOutline";

            // Shader uniforms
            private const string INPUT_TEXTURE = "InputTexture";
            private const string DEPTH_TEXTURE = "DepthTexture";
            private const string OUTPUT_TEXTURE = "OutputTexture";
            private const string NORMAL_TEXTURE = "NormalTexture";

            public bool CanCompute => _sobelComputeShader != null;

            public SobelComputer(CommandBuffer cmd, RenderTextureDescriptor descriptor)
            {
                InitializeShader(cmd, descriptor);

#if UNITY_EDITOR
                ComputeShaderPostprocessor.AddImportHandler(_sobelComputeShader, shader =>
                {
                    Debug.Log("SobelComputer::ComputeShaderPostprocessor: Shader reloaded");
                    Free();
                    InitializeShader(cmd, descriptor);
                    SetTargets(cmd, _sobelTargets);
                });
#endif
            }

            private void InitializeShader(CommandBuffer cmd, RenderTextureDescriptor descriptor)
            {
                _sobelComputeShader = Resources.Load<ComputeShader>(_shaderPath);
                _mainKernel = _sobelComputeShader.FindKernel("CSMain");
                Resolution = new Vector2Int(descriptor.width, descriptor.height);
            }

            public void SetTargets(CommandBuffer cmd, ShaderTargets targets)
            {
                _sobelTargets = targets;

                cmd.SetComputeTextureParam(_sobelComputeShader, _mainKernel, INPUT_TEXTURE, targets.ColorTarget);
                cmd.SetComputeTextureParam(_sobelComputeShader, _mainKernel, DEPTH_TEXTURE, targets.DepthTarget);
                cmd.SetComputeTextureParam(_sobelComputeShader, _mainKernel, OUTPUT_TEXTURE, targets.OutputTarget);
                cmd.SetComputeTextureParam(_sobelComputeShader, _mainKernel, NORMAL_TEXTURE, targets.NormalTarget);
            }

            public void SetTargets(CommandBuffer cmd, RenderTargetIdentifier colorTarget, RenderTargetIdentifier depthTarget, RenderTargetIdentifier outputTarget)
            {
                _sobelTargets = new ShaderTargets
                {
                    ColorTarget = colorTarget,
                    DepthTarget = depthTarget,
                    OutputTarget = outputTarget
                };

                SetTargets(cmd, _sobelTargets);
            }

            public RenderTargetIdentifier Compute(CommandBuffer cmd, RenderTextureDescriptor descriptor)
            {
                Vector3Int groupCount = new Vector3Int(
                    Mathf.CeilToInt(Resolution.x / (float)_groups.x),
                    Mathf.CeilToInt(Resolution.y / (float)_groups.y),
                    _groups.z
                );

                // Debug.Log("SobelComputer::Compute: Dispatching compute shader with group count: " + groupCount.ToString() + " and resolution: " + Resolution.ToString());
                cmd.DispatchCompute(_sobelComputeShader, _mainKernel, groupCount.x, groupCount.y, groupCount.z);
                return _sobelTargets.OutputTarget;
            }

            public void Free()
            {
                Debug.Log("SobelComputer::Free");
            }
        }

        private SobelOutlineComponent _settings;
        private SobelComputer _computer;
        private SobelComputer.ShaderTargets _inputTargets;

        private RenderTargetIdentifier _outputIdentifier;
        private int _OUTPUT_ID => Shader.PropertyToID("_SobelOutlinePass_Output");

        public SobelOutlinePass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Fetch the settigs from the volume
            if (_settings == null)
            {
                _settings = VolumeManager.instance.stack.GetComponent<SobelOutlineComponent>();
            }
            if (_settings == null) return;
            if (_settings.IsActive() == false) return;
   
            // Debug.Log("SobelOutlinePass::OnCameraSetup with camera: " + renderingData.cameraData.camera.name);
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.enableRandomWrite = true;

            // create the sobel computer for the camera, if needed
            if (_computer == null)
            {
                _computer = new SobelComputer(cmd, descriptor);
                _outputIdentifier = new RenderTargetIdentifier(_OUTPUT_ID);
            }

            _computer.Resolution = new Vector2Int(descriptor.width, descriptor.height);

            cmd.GetTemporaryRT(_OUTPUT_ID, descriptor.width, descriptor.height, descriptor.depthBufferBits, FilterMode.Bilinear,
                                descriptor.colorFormat, RenderTextureReadWrite.Default, 1, true, descriptor.memoryless);

            _inputTargets = new SobelComputer.ShaderTargets
            {
                ColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle,
                DepthTarget = Shader.GetGlobalTexture("_CameraDepthTexture"),
                NormalTarget = Shader.GetGlobalTexture("_CameraNormalsTexture"),
                OutputTarget = _outputIdentifier
            };
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Fetch the settigs from the volume
            if (_settings == null)
            {
                _settings = VolumeManager.instance.stack.GetComponent<SobelOutlineComponent>();
            }
            if (_settings == null) return;
            if (_settings.IsActive() == false) return;

            // grab our computer
            if (_computer == null || _computer.CanCompute == false)
            {
                Debug.LogWarning("SobelOutlinePass::Execute: SobelComputer cannot compute. Check for valid shader in resources!");
                return;
            }

            // Debug.Log("SobelOutlinePass::Execute with camera: " + renderingData.cameraData.camera.name);
            CommandBuffer cmd = CommandBufferPool.Get("SobelOutlinePass");
            using (new ProfilingScope(cmd, new ProfilingSampler("SobelOutlinePass::Compute")))
            {
                _inputTargets.ColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
                _inputTargets.DepthTarget = Shader.GetGlobalTexture("_CameraDepthTexture");
                _inputTargets.NormalTarget = Shader.GetGlobalTexture("_CameraNormalsTexture");

                _computer.SetTargets(cmd, _inputTargets);

                // Compute the sobel
                RenderTargetIdentifier output = _computer.Compute(cmd, renderingData.cameraData.cameraTargetDescriptor);
                ConfigureTarget(output);
                // Blit the sobel to the camera target
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