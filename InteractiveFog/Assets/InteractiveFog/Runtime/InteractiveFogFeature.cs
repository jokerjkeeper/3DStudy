using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace InteractiveFog
{
    /// <summary>
    /// URP (Unity 6, RenderGraph) renderer feature: raymarches the fog layer over the camera colour,
    /// using the camera depth texture to stop rays at scene geometry.
    /// Only active while a FogSimulation is running.
    /// </summary>
    public class InteractiveFogFeature : ScriptableRendererFeature
    {
        public Shader shader;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        Material _material;
        FogPass _pass;

        public override void Create()
        {
            if (shader == null) shader = Shader.Find("Hidden/InteractiveFog/Raymarch");
            if (shader != null) _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new FogPass(_material) { renderPassEvent = passEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || FogSimulation.Active == null) return;
            var type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }

        class FogPass : ScriptableRenderPass
        {
            readonly Material _mat;

            class PassData
            {
                public TextureHandle source;
                public Material material;
            }

            public FogPass(Material mat)
            {
                _mat = mat;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle src = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(src);
                desc.name = "_InteractiveFogColor";
                desc.clearBuffer = false;
                TextureHandle dst = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Interactive Fog", out var data))
                {
                    data.source = src;
                    data.material = _mat;
                    builder.UseTexture(src, AccessFlags.Read);
                    if (resources.cameraDepthTexture.IsValid())
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0));
                }

                resources.cameraColor = dst;
            }
        }
    }
}
