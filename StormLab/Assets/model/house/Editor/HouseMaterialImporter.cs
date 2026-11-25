using System;
using UnityEditor;
using UnityEngine;

namespace StormLab.Editor
{
    internal sealed class HouseMaterialImporter : AssetPostprocessor
    {
        private const string HouseModel = "Assets/model/house/source/SM_House.fbx";
        private const string HouseMaterial = "Assets/model/house/House URP.mat";

        private void OnPostprocessModel(GameObject root)
        {
            if (!string.Equals(assetPath, HouseModel, StringComparison.Ordinal))
                return;

            context.DependsOnArtifact(HouseMaterial);
            var material = AssetDatabase.LoadAssetAtPath<Material>(HouseMaterial);
            if (!material)
                return;

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = material;
        }
    }
}
