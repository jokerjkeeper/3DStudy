using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StormLab.Editor
{
    static class CityMotionSetup
    {
        const string MeshPath = "Assets/StormLab/City/Perlica Motion.asset";

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(CyberpunkCityBuilder.ScenePath);
            var model = GameObject.Find("Perlica(patreon) (1)").transform;
            var sourceRenderers = model.GetComponentsInChildren<Renderer>();
            if (model.Find("Perlica motion rig") != null)
                throw new InvalidOperationException("Perlica already has the city motion rig.");


            var root = Pivot(model, "Perlica motion rig", Vector3.zero);
            var torso = Pivot(root, "Torso", new Vector3(0, 1.02f, -0.025f));
            var head = Pivot(torso, "Head", new Vector3(0, 0.37f, 0.01f));
            var left = Pivot(torso, "Left upper arm", new Vector3(-0.145f, 0.247f, -0.008f));
            var right = Pivot(torso, "Right upper arm", new Vector3(0.145f, 0.247f, -0.008f));
            var leftElbow = Pivot(left, "Left forearm", new Vector3(-0.177f, -0.143f, 0f));
            var rightElbow = Pivot(right, "Right forearm", new Vector3(0.177f, -0.143f, 0f));
            var bones = new[] { root, torso, head, left, right, leftElbow, rightElbow };
            var meshes = ScriptableObject.CreateInstance<CityTargetMeshes>();
            AssetDatabase.CreateAsset(meshes, MeshPath);
            foreach (var renderer in sourceRenderers) Bind(model, renderer, bones, meshes);
            AssetDatabase.SaveAssets();

            var motion = root.gameObject.AddComponent<CityTargetMotion>();
            motion.Configure(torso, head, left, right, leftElbow, rightElbow);
            var pose = GameObject.Find("City duel choreography").GetComponent<CityDuelPose>();
            pose.ConfigureTargetMotion(motion);

            var key = GameObject.Find("Twilight key").GetComponent<Light>();
            key.transform.rotation = Quaternion.Euler(49f, 42f, 0f);
            key.intensity = 1.12f;
            key.shadowStrength = 0.77f;
            SetSpill("Cool street spill", new Vector3(-3.2f, 2.45f, 4.4f), 17f);
            SetSpill("Magenta billboard spill", new Vector3(3.8f, 2.65f, 3.7f), 15f);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        static void SetSpill(string name, Vector3 position, float intensity)
        {
            var light = GameObject.Find(name).GetComponent<Light>();
            light.transform.position = position;
            light.range = 12f;
            light.intensity = intensity;
        }

        static void Bind(Transform model, Renderer source, Transform[] bones, CityTargetMeshes meshes)
        {
            var skinned = source as SkinnedMeshRenderer;
            var original = skinned != null ? skinned.sharedMesh : source.GetComponent<MeshFilter>()?.sharedMesh;
            if (original == null) return;
            bool armGeometry = source.name is "body" or "jacket" or "gloves" or "watch";
            var mesh = Object.Instantiate(original);
            mesh.name = source.name + " (Perlica motion)";
            Vector3[] vertices = original.vertices;
            Matrix4x4 toModel = model.worldToLocalMatrix * source.localToWorldMatrix;
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                weights[i] = Weigh(toModel.MultiplyPoint3x4(vertices[i]), armGeometry);
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bind[i] = bones[i].worldToLocalMatrix * source.localToWorldMatrix;
            mesh.bindposes = bind;
            mesh.boneWeights = weights;
            AssetDatabase.AddObjectToAsset(mesh, meshes);

            if (skinned == null)
            {
                var replacement = Pivot(source.transform, "Animated surface", Vector3.zero);
                skinned = replacement.gameObject.AddComponent<SkinnedMeshRenderer>();
                skinned.sharedMaterials = source.sharedMaterials;
                skinned.shadowCastingMode = source.shadowCastingMode;
                skinned.receiveShadows = source.receiveShadows;
                skinned.lightProbeUsage = source.lightProbeUsage;
                skinned.reflectionProbeUsage = source.reflectionProbeUsage;
                source.enabled = false;
            }
            skinned.sharedMesh = mesh;
            skinned.rootBone = bones[0];
            skinned.bones = bones;
            // Keep skinned surfaces visible through the full guard and flinch range.
            skinned.updateWhenOffscreen = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            PrefabUtility.RecordPrefabInstancePropertyModifications(skinned);
        }

        static BoneWeight Weigh(Vector3 p, bool armGeometry)
        {
            float x = Mathf.Abs(p.x);
            float arm = armGeometry
                ? Ramp(0.145f, 0.208f, x) * Ramp(0.87f, 0.94f, p.y) * (1f - Ramp(1.30f, 1.38f, p.y))
                : 0f;
            float trunk = Ramp(0.78f, 1.09f, p.y);
            if (arm > 0f)
            {
                float elbow = Ramp(0.276f, 0.365f, x);
                return Sorted(p.x < 0f ? 3 : 4, arm * (1f - elbow), p.x < 0f ? 5 : 6, arm * elbow,
                    1, trunk * (1f - arm), 0, (1f - trunk) * (1f - arm));
            }
            float neck = Ramp(1.31f, 1.43f, p.y);
            return Sorted(2, neck, 1, trunk * (1f - neck), 0, (1f - trunk) * (1f - neck), 0, 0f);
        }

        static BoneWeight Sorted(int a, float wa, int b, float wb, int c, float wc, int d, float wd)
        {
            SwapIfLower(ref a, ref wa, ref b, ref wb);
            SwapIfLower(ref c, ref wc, ref d, ref wd);
            SwapIfLower(ref a, ref wa, ref c, ref wc);
            SwapIfLower(ref b, ref wb, ref d, ref wd);
            SwapIfLower(ref b, ref wb, ref c, ref wc);
            return new BoneWeight
            {
                boneIndex0 = a, weight0 = wa, boneIndex1 = b, weight1 = wb,
                boneIndex2 = c, weight2 = wc, boneIndex3 = d, weight3 = wd
            };
        }

        static void SwapIfLower(ref int a, ref float wa, ref int b, ref float wb)
        {
            if (wa >= wb) return;
            (a, b) = (b, a);
            (wa, wb) = (wb, wa);
        }

        static float Ramp(float min, float max, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));

        static Transform Pivot(Transform parent, string name, Vector3 position)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = position;
            return bone;
        }
    }
}
