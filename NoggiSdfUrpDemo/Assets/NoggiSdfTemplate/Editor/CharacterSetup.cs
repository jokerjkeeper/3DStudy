using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NoggiSdfToolkit.Editor
{
    public sealed class CharacterSetup : EditorWindow
    {
        const string MaterialRoot = "Assets/NoggiSdfTemplate/Materials/";
        const string OutlineName = "Noggi Inverted Hull";
        Renderer target;
        Material face;
        Material outline;
        Transform basis;
        int slot;
        bool outlineOnlySlot;

        [MenuItem("Tools/Noggi SDF/Character Setup")]
        static void Open()
        {
            var window = GetWindow<CharacterSetup>("Noggi SDF Setup");
            if (Selection.activeGameObject)
                window.target = Selection.activeGameObject.GetComponent<Renderer>();
            if (!window.outline) window.outline = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "Outline Template.mat");
            window.Show();
        }

        void OnGUI()
        {
            target = (Renderer)EditorGUILayout.ObjectField("Target Renderer", target, typeof(Renderer), true);
            int slotCount = target ? target.sharedMaterials.Length : 0;
            if (slotCount > 0)
            {
                Material[] materials = target.sharedMaterials;
                var labels = new string[slotCount];
                for (int i = 0; i < labels.Length; i++)
                    labels[i] = $"{i} · {(materials[i] ? materials[i].name : "Empty")}";
                slot = EditorGUILayout.Popup("Material Slot", Mathf.Clamp(slot, 0, slotCount - 1), labels);
            }

            EditorGUILayout.Space();
            face = (Material)EditorGUILayout.ObjectField("Face Material", face, typeof(Material), false);
            basis = (Transform)EditorGUILayout.ObjectField("Head Basis (optional)", basis, typeof(Transform), true);
            using (new EditorGUI.DisabledScope(!target || slotCount == 0 || !face || !face.HasProperty("_FaceSdf")))
            {
                if (GUILayout.Button("Assign Face Material"))
                    AssignFace(target, slot, face, basis);
            }

            EditorGUILayout.Space();
            outline = (Material)EditorGUILayout.ObjectField("Outline Material", outline, typeof(Material), false);
            Mesh mesh = SourceMesh(target);
            outlineOnlySlot = EditorGUILayout.Toggle("Only This Material Slot", outlineOnlySlot);
            var skip = AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + "Skip Outline Slot.mat");
            using (new EditorGUI.DisabledScope(!mesh || !outline || !outline.HasProperty("_OutlineWidth") || (outlineOnlySlot && (!skip || slot >= mesh.subMeshCount))))
            {
                if (GUILayout.Button("Create or Update Inverted Hull"))
                    CreateOutline(target, outline, skip, outlineOnlySlot ? slot : -1);
            }
        }

        public static void AssignFace(Renderer renderer, int materialSlot, Material material, Transform headBasis)
        {
            var slots = renderer.sharedMaterials;
            if (materialSlot < 0 || materialSlot >= slots.Length)
                throw new ArgumentOutOfRangeException(nameof(materialSlot));
            Undo.RecordObject(renderer, "Assign SDF Face");
            slots[materialSlot] = material;
            renderer.sharedMaterials = slots;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

            if (headBasis || renderer.TryGetComponent<FaceSdfBasis>(out _))
            {
                var binding = renderer.GetComponent<FaceSdfBasis>();
                if (!binding)
                    binding = Undo.AddComponent<FaceSdfBasis>(renderer.gameObject);
                Undo.RecordObject(binding, "Set SDF Head Basis");
                binding.Bind(renderer, materialSlot, headBasis);
                PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
            }
        }

        static Mesh SourceMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned && skinned)
                return skinned.sharedMesh;
            if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
                return filter.sharedMesh;
            return null;
        }

        public static Renderer CreateOutline(Renderer source, Material ink, Material skip, int onlySlot = -1)
        {
            var skinned = source as SkinnedMeshRenderer;
            var mesh = SourceMesh(source);
            if (!mesh)
                throw new ArgumentException("Select a MeshRenderer or SkinnedMeshRenderer with a mesh.", nameof(source));
            if (onlySlot >= 0 && (!skip || onlySlot >= mesh.subMeshCount))
                throw new ArgumentOutOfRangeException(nameof(onlySlot));

            var existing = source.transform.Find(OutlineName);
            var child = existing ? existing.gameObject : new GameObject(OutlineName);
            if (!existing)
            {
                child.layer = source.gameObject.layer;
                Undo.RegisterCreatedObjectUndo(child, "Create Inverted Hull");
                Undo.SetTransformParent(child.transform, source.transform, "Parent Inverted Hull");
                child.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                child.transform.localScale = Vector3.one;
            }

            Renderer result;
            if (skinned)
            {
                var copy = child.GetComponent<SkinnedMeshRenderer>();
                if (!copy) copy = Undo.AddComponent<SkinnedMeshRenderer>(child);
                Undo.RecordObject(copy, "Set Inverted Hull Skin");
                copy.sharedMesh = mesh;
                copy.bones = skinned.bones;
                copy.rootBone = skinned.rootBone;
                copy.localBounds = skinned.localBounds;
                copy.quality = skinned.quality;
                copy.updateWhenOffscreen = skinned.updateWhenOffscreen;
                if (mesh.blendShapeCount > 0)
                {
                    var sync = child.GetComponent<OutlineBlendShapes>();
                    if (!sync) sync = Undo.AddComponent<OutlineBlendShapes>(child);
                    Undo.RecordObject(sync, "Bind Outline Blend Shapes");
                    sync.Bind(skinned, copy);
                }
                result = copy;
            }
            else
            {
                var filter = child.GetComponent<MeshFilter>();
                if (!filter) filter = Undo.AddComponent<MeshFilter>(child);
                Undo.RecordObject(filter, "Set Inverted Hull Mesh");
                filter.sharedMesh = mesh;
                result = child.GetComponent<MeshRenderer>();
                if (!result) result = Undo.AddComponent<MeshRenderer>(child);
            }

            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = onlySlot < 0 || i == onlySlot ? ink : skip;
            Undo.RecordObject(result, "Set Inverted Hull Material");
            result.sharedMaterials = materials;
            result.shadowCastingMode = ShadowCastingMode.Off;
            result.receiveShadows = false;
            result.lightProbeUsage = LightProbeUsage.Off;
            result.reflectionProbeUsage = ReflectionProbeUsage.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(result);
            return result;
        }
    }
}


