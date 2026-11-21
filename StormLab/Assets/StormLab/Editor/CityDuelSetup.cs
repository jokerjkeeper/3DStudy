using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StormLab.Editor
{
    static class CityDuelSetup
    {
        // Uses the actual pasted prefab instances and leaves their source assets available for reimport.
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(CyberpunkCityBuilder.ScenePath);
            var roots = scene.GetRootGameObjects();
            var ember = roots.Single(item => item.name == "arknights_endfield_-ember-_unityexport").transform;
            var perlica = roots.Single(item => item.name == "Perlica(patreon) (1)").transform;
            var controls = roots.Select(item => item.GetComponent<StormControls>()).Single(item => item != null);
            var settings = new SerializedObject(controls);
            var oldPose = (GrovePose)settings.FindProperty("battlePose").objectReferenceValue;
            var oldBinding = new SerializedObject(oldPose);
            var oldCaster = ((Transform)oldBinding.FindProperty("casterBody").objectReferenceValue).parent;
            var oldTarget = ((Transform)oldBinding.FindProperty("sentinelBody").objectReferenceValue).parent;
            var sequence = controls.Sequence;
            var camera = (Camera)settings.FindProperty("view").objectReferenceValue;
            var ground = GameObject.Find("City road and pavement").GetComponent<MeshCollider>();
            var caster = Pivot(null, "Ember • caster");
            caster.rotation = Quaternion.Euler(0f, 197f, 0f);
            Place(ember, caster, oldCaster.position, Quaternion.Euler(-90f, 197f, 0f), ground);

            var target = Pivot(null, "Perlica • target");
            var recoil = Pivot(target, "Hit reaction");
            Place(perlica, recoil, oldTarget.position, Quaternion.Euler(0f, 153f, 0f), ground);
            var receiver = Pivot(recoil, "Perlica hit surface").gameObject.AddComponent<CapsuleCollider>();
            receiver.center = Vector3.up * 0.86f;
            receiver.height = 1.68f;
            receiver.radius = 0.23f;
            var aim = Pivot(receiver.transform, "Chest aim point");
            aim.localPosition = Vector3.up * 1.15f;

            var clavicle = Bone(ember, "_0308");
            var upper = Bone(ember, "_0309");
            var elbow = Bone(ember, "_0310");
            var wrist = Bone(ember, "_0312");
            var knuckle = Bone(ember, "_0321");
            var front = caster.forward;
            var palm = Anchor(wrist, "Ember palm anchor", Vector3.Lerp(wrist.position, knuckle.position, 0.68f) + front * 0.045f);

            var oldHand = sequence.Hand.transform;
            oldHand.SetParent(caster, false);
            oldHand.localScale = Vector3.one;
            var vfx = sequence.Hand.Palm;
            // Parent the existing fixture at unit scale. The pose drives its actual emission transform from the wrist.
            var pose = Pivot(null, "City duel choreography").gameObject.AddComponent<CityDuelPose>();
            pose.Configure(sequence, new CityDuelPose.Arm(upper, elbow, wrist),
                new CityDuelPose.Arm(Bone(ember, "_0233"), Bone(ember, "_0255"), Bone(ember, "_0257")),
                caster, palm, vfx, recoil, aim, receiver);

            var collar = Anchor(clavicle, "Current collar", clavicle.position + front * 0.091f + Vector3.up * 0.07f);
            var shoulder = Anchor(upper, "Current shoulder", upper.position + front * 0.112f + Vector3.up * 0.038f);
            var forearm = Anchor(elbow, "Current forearm", Vector3.Lerp(elbow.position, wrist.position, 0.35f) + front * 0.088f);
            var holder = Pivot(caster, "Ember body current");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/StormLab/Grove/BodyCurrent/Electric arc.mat");
            GroveEffectsSetup.Arc(holder, "Collar to shoulder", collar, shoulder, material, 0.043f, 61, new Vector2(0f, 0.34f));
            GroveEffectsSetup.Arc(holder, "Shoulder to forearm", shoulder, forearm, material, 0.048f, 27, new Vector2(0.34f, 0.68f));
            GroveEffectsSetup.Arc(holder, "Forearm to palm", forearm, palm, material, 0.045f, 73, new Vector2(0.68f, 0.96f));

            var current = camera.GetComponent<GroveBodyCurrent>();
            int layer = LayerMask.NameToLayer("GroveCaster");
            if (layer < 0) throw new InvalidOperationException("The Storm Lab body-current layer is missing.");
            foreach (var renderer in ember.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.gameObject.layer = layer;
            current.Configure(sequence, holder.gameObject);
            settings.FindProperty("testTarget").objectReferenceValue = aim;
            settings.FindProperty("muzzle").objectReferenceValue = vfx;
            settings.FindProperty("emitter").objectReferenceValue = null;
            settings.FindProperty("battlePose").objectReferenceValue = pose;
            settings.ApplyModifiedPropertiesWithoutUndo();

            oldPose.gameObject.SetActive(false);
            oldCaster.gameObject.SetActive(false);
            oldTarget.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(scene);
        }

        static void Place(Transform model, Transform pivot, Vector3 oldPosition, Quaternion facing, Collider ground)
        {
            float z = oldPosition.z;
            var xz = new Vector3(oldPosition.x, 0f, z);
            if (!ground.Raycast(new Ray(xz + Vector3.up * 3f, Vector3.down), out var hit, 6f))
                throw new InvalidOperationException("Could not ground " + model.name);

            // Keep the new character model's authored scale and put the pivot at street level.
            pivot.root.position = new Vector3(xz.x, hit.point.y, xz.z);
            model.SetParent(pivot, false);
            model.SetPositionAndRotation(pivot.position, facing);
            float feet = model.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.y);
            model.position += Vector3.up * (hit.point.y - feet);
        }

        static Transform Bone(Transform model, string suffix) => model.GetComponentsInChildren<Transform>(true)
            .Single(bone => bone.name.EndsWith(suffix, StringComparison.Ordinal));

        static Transform Anchor(Transform bone, string name, Vector3 worldPosition)
        {
            var anchor = Pivot(bone, name);
            anchor.position = worldPosition;
            return anchor;
        }

        static Transform Pivot(Transform parent, string name)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            return pivot;
        }
    }
}
