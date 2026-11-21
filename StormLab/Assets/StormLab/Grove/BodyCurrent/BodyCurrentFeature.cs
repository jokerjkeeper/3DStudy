using System.Collections.Generic;
using StormLab;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BodyCurrent
{
    public sealed class BodyCurrentFeature : ScriptableRendererFeature
    {
        public LayerMask characterLayer;
        public Shader maskShader;
        public Shader compositeShader;
        [Range(1, 6)] public float outlinePixels = 2.4f;
        [Range(0, 1)] public float fresnelStrength = 0.38f;

        Material maskMaterial;
        Material compositeMaterial;
        CurrentPass pass;

        public override void Create()
        {
            ReleaseMaterials();
            if (!maskShader || !compositeShader)
                return;

            maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
            pass = new CurrentPass(this) { renderPassEvent = RenderPassEvent.AfterRenderingOpaques };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (pass != null && data.cameraData.cameraType == CameraType.Game &&
                data.cameraData.camera.TryGetComponent<GroveBodyCurrent>(out var current) && current.IsVisible)
                renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => ReleaseMaterials();

        void ReleaseMaterials()
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(compositeMaterial);
            pass = null;
        }

        sealed class CurrentPass : ScriptableRenderPass
        {
            static readonly List<ShaderTagId> Tags = new()
            {
                new("UniversalForward"), new("UniversalForwardOnly"), new("SRPDefaultUnlit")
            };
            readonly BodyCurrentFeature settings;

            sealed class MaskData
            {
                public RendererListHandle renderers;
            }

            sealed class CompositeData
            {
                public TextureHandle mask;
                public Material material;
            }

            public CurrentPass(BodyCurrentFeature settings) => this.settings = settings;

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var camera = frame.Get<UniversalCameraData>();
                var rendering = frame.Get<UniversalRenderingData>();
                var lights = frame.Get<UniversalLightData>();
                var descriptor = camera.cameraTargetDescriptor;

                // This sample pairs the mask with the camera depth attachment (MSAA off).
                if (descriptor.msaaSamples != 1 || !resources.activeDepthTexture.IsValid())
                    return;

                var maskDescriptor = graph.GetTextureDesc(resources.activeDepthTexture);
                maskDescriptor.name = "Body Current Mask";
                maskDescriptor.format = GraphicsFormat.R8G8_UNorm;
                maskDescriptor.clearBuffer = true;
                maskDescriptor.clearColor = Color.clear;
                var mask = graph.CreateTexture(maskDescriptor);
                var draw = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
                draw.overrideMaterial = settings.maskMaterial;
                var filter = new FilteringSettings(RenderQueueRange.opaque, settings.characterLayer);
                var renderers = graph.CreateRendererList(new RendererListParams(rendering.cullResults, draw, filter));

                using (var builder = graph.AddRasterRenderPass<MaskData>("Body Current / Character Mask", out var data))
                {
                    data.renderers = renderers;
                    builder.UseRendererList(renderers);
                    builder.SetRenderAttachment(mask, 0);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc(static (MaskData item, RasterGraphContext context) =>
                        context.cmd.DrawRendererList(item.renderers));
                }

                settings.compositeMaterial.SetFloat("_OutlinePixels", settings.outlinePixels);
                settings.compositeMaterial.SetFloat("_FresnelStrength", settings.fresnelStrength);
                settings.compositeMaterial.SetVector("_MaskPixel", new Vector4(1f / descriptor.width, 1f / descriptor.height, 0, 0));

                using (var builder = graph.AddRasterRenderPass<CompositeData>("Body Current / Composite", out var data))
                {
                    data.mask = mask;
                    data.material = settings.compositeMaterial;
                    builder.UseTexture(mask);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (CompositeData item, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, item.mask, new Vector4(1, 1, 0, 0), item.material, 0));
                }
            }
        }
    }
}
