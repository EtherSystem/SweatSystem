using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SweatSystem.Mod), "SweatSystem", "1.0.0", "EtherSystem")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace SweatSystem
{
    public class Mod : MelonMod
    {
        private float _tick;
        private const float TICK_SECONDS = 5f;            // application frequency
        private const float START_C = 30f;                // temperature threshold before sweating begins
        private const float MAX_C = 50f;                  // temperature threshold at which perspiration reaches its maximum
        private const float MIN_WETNESS_PER_TICK = 0.5f;  // Min Wetness % per tick
        private const float MAX_WETNESS_PER_TICK = 5f;    // Max Wetness % per tick

        private static readonly ClothingRegion[] Regions =
        {
            ClothingRegion.Head,
            ClothingRegion.Chest,
            ClothingRegion.Hands,
            ClothingRegion.Legs,
            ClothingRegion.Feet
        };

        public override void OnUpdate()
        {
            if (GameManager.m_Instance == null || GameManager.m_IsPaused) return;

            string scene = GameManager.m_ActiveScene;
            if (scene == "MainMenu" || scene == "Boot" || scene == "Empty") return;

            _tick += Time.deltaTime;
            if (_tick < TICK_SECONDS) return;
            _tick = 0f;

            var pm = GameManager.GetPlayerManagerComponent();
            var wc = GameManager.GetWeatherComponent();
            if (pm == null || wc == null) return;

            float airTemp = wc.GetCurrentTemperature();
            float clothingBonus = pm.m_WarmthBonusFromClothing;
            float windChill = wc.GetCurrentWindchill();
            float clothingWindBonus = pm.m_WindproofBonusFromClothing;
            float netWindChill = Mathf.Min(windChill + clothingWindBonus, 0f);
            float feelsLikeC = airTemp + clothingBonus + netWindChill;

            float hot01 = Mathf.Clamp01(Mathf.InverseLerp(START_C, MAX_C, feelsLikeC));
            if (hot01 <= 0f) return;

            float amount = Mathf.Lerp(MIN_WETNESS_PER_TICK, MAX_WETNESS_PER_TICK, hot01);

            ApplySweat(pm, amount);
        }

        private static void ApplySweat(PlayerManager pm, float amount)
        {
            foreach (var region in Regions)
            {
                float remaining = amount;

                //lower layers first
                remaining = AddWetnessUpTo(pm, region, ClothingLayer.Base, remaining, 80f);
                if (remaining <= 0f) continue;

                remaining = AddWetnessUpTo(pm, region, ClothingLayer.Mid, remaining, 60f);
                if (remaining <= 0f) continue;

                remaining = AddWetnessUpTo(pm, region, ClothingLayer.Top, remaining, 40f);
                if (remaining <= 0f) continue;

                AddWetnessUpTo(pm, region, ClothingLayer.Top2, remaining, 30f);
            }
        }

        private static float AddWetnessUpTo(PlayerManager pm, ClothingRegion region, ClothingLayer layer, float add, float cap)
        {
            if (add <= 0f) return 0f;

            var gi = pm.GetClothingInSlot(region, layer);
            if (gi == null || gi.m_ClothingItem == null) return add;

            var ci = gi.m_ClothingItem;

            float before = ci.m_PercentWet;
            if (before >= cap) return add;

            float wanted = Mathf.Min(add, cap - before);
            if (wanted <= 0f) return add;

            float target = Mathf.Min(before + wanted, cap);
            ci.m_PercentWet = target;

            float after = ci.m_PercentWet;
            float delta = after - before;

            if (delta <= 0.0001f) return add;

            return add - delta;
        }
    }
}