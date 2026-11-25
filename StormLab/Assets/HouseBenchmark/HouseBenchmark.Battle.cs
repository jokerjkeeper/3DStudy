using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StormLab.Benchmark
{
    public sealed partial class HouseBenchmark
    {
        [SerializeField] GameObject cityBattleRig;
        [SerializeField] UniversalRenderPipelineAsset cityBattlePipeline;

        GameObject battle;
        GameObject frontHouse;
        GameObject battleGlass;
        Material battleGlassMaterial;
        Light benchmarkSun;
        bool benchmarkSunEnabled;

        void StartCityBattle()
        {
            ClearLayout();
            SetPipeline(cityBattlePipeline);
            Floor(46);
            var groundCollider = floor.AddComponent<BoxCollider>();
            groundCollider.center = new Vector3(0f, -.06f, 0f);
            groundCollider.size = new Vector3(10f, .12f, 10f);

            PutBattleHouse(new Vector3(-7.6f, 0f, 1.0f), 90f);
            PutBattleHouse(new Vector3(7.6f, 0f, 2.8f), -90f);
            PutBattleHouse(new Vector3(-4.6f, 0f, 10.5f), 0f);
            PutBattleHouse(new Vector3(4.6f, 0f, 11.7f), 0f);
            StaticBatchingUtility.Combine(block);

            // Visual occluders do not intercept the city rig's test-shot raycast.
            frontHouse = PutMesh(block.transform, whole, material4K).gameObject;
            frontHouse.transform.SetLocalPositionAndRotation(new Vector3(-4.65f, 0f, -4.5f), Quaternion.Euler(0f, 12f, 0f));
            frontHouse.SetActive(false);
            battleGlassMaterial ??= Transparent(new Color(.16f, .58f, 1f, .33f));
            battleGlass = Primitive(PrimitiveType.Quad, "Transparent foreground glass", battleGlassMaterial,
                new Vector3(-1.05f, 1.25f, -2.6f), new Vector3(2.35f, 2.5f, 1f)).gameObject;
            battleGlass.SetActive(false);

            benchmarkSun = RenderSettings.sun;
            benchmarkSunEnabled = benchmarkSun && benchmarkSun.enabled;
            if (benchmarkSun) benchmarkSun.enabled = false;
            view.enabled = false;
            battle = Instantiate(cityBattleRig);
            battle.name = "Cyberpunk City battle";
            RenderSettings.sun = battle.transform.Find("Twilight key").GetComponent<Light>();
            battle.SetActive(true);
            Physics.SyncTransforms();
        }

        void PutBattleHouse(Vector3 position, float yaw)
        {
            var house = PutMesh(block.transform, whole, material4K);
            house.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            house.gameObject.AddComponent<MeshCollider>().sharedMesh = whole;
        }

        void DrawCityBattle()
        {
            const float width = 400f;
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, 8f, width, 84f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            bool back = GUILayout.Button("Back to benchmark");
            bool house = GUILayout.Toggle(frontHouse.activeSelf, "Front house");
            bool glass = GUILayout.Toggle(battleGlass.activeSelf, "Glass");
            GUILayout.EndHorizontal();
            GUILayout.Label("C  Preview   F  Play sequence   Space  Hit target   R  Reset");
            GUILayout.Label("WASD  Move   Q/E  Height   RMB  Look   Wheel  FOV   Home  Reset view");
            GUILayout.EndArea();

            if (back)
            {
                StopCityBattle();
                lab = 0;
                Configure(selectedGroup, selectedCount);
                return;
            }
            if (house != frontHouse.activeSelf) frontHouse.SetActive(house);
            if (glass != battleGlass.activeSelf) battleGlass.SetActive(glass);
        }

        void StopCityBattle()
        {
            if (!battle) return;

            battle.SetActive(false);
            Destroy(battle);
            battle = null;
            if (benchmarkSun) benchmarkSun.enabled = benchmarkSunEnabled;
            RenderSettings.sun = benchmarkSun;
            if (view) view.enabled = true;
        }
    }
}
