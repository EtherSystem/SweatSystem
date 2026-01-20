using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(SweatSystem.Mod), "SweatSystem", "1.0.0", "EtherSystem")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace SweatSystem
{
    public class Mod : MelonMod
    {
        private const float TICK_SECONDS = 5f;              // application frequency
        private const float START_C = 30f;                  // temperature threshold before sweating begins
        private const float MAX_C = 50f;                    // temperature threshold at which perspiration reaches its maximum
        private const float MIN_WETNESS_PER_TICK = 0.02f;   // Min Wetness % per tick
        private const float MAX_WETNESS_PER_TICK = 0.2f;    // Max Wetness % per tick
        private float _realAccum = 0f;
        private double _gameSecondsAccum = 0.0;

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
            if (GameManager.m_Instance == null) return;

            string scene = GameManager.m_ActiveScene;
            if (scene == "MainMenu" || scene == "Boot" || scene == "Empty") return;

            var pm = GameManager.GetPlayerManagerComponent();
            var wc = GameManager.GetWeatherComponent();
            var tod = GameManager.GetTimeOfDayComponent();
            if (pm == null || wc == null || tod == null) return;

            float airTemp = wc.GetCurrentTemperature();
            float clothingBonus = pm.m_WarmthBonusFromClothing;
            float windChill = wc.GetCurrentWindchill();
            float clothingWindBonus = pm.m_WindproofBonusFromClothing;
            float netWindChill = Mathf.Min(windChill + clothingWindBonus, 0f);
            float feelsLikeC = airTemp + clothingBonus + netWindChill;

            float hot01 = Mathf.Clamp01(Mathf.InverseLerp(START_C, MAX_C, feelsLikeC));
            if (hot01 <= 0f)
            {
                _realAccum = 0f;
                _gameSecondsAccum = 0.0;
                return;
            }

            _realAccum += Time.unscaledDeltaTime;
            if (_realAccum < 0.25f) return;

            float realElapsed = _realAccum;
            _realAccum = 0f;

            float gameHoursPassed = tod.GetTODHours(realElapsed);
            if (gameHoursPassed <= 0f) return;

            if (gameHoursPassed > 12f)
            {
                _gameSecondsAccum = 0.0;
                return;
            }

            _gameSecondsAccum += (double)gameHoursPassed * 3600.0;

            if (_gameSecondsAccum < TICK_SECONDS) return;

            int ticks = (int)(_gameSecondsAccum / TICK_SECONDS);
            _gameSecondsAccum -= ticks * TICK_SECONDS;

            float perTick = Mathf.Lerp(MIN_WETNESS_PER_TICK, MAX_WETNESS_PER_TICK, hot01);
            float totalAmount = perTick * ticks;

            ApplySweat(pm, totalAmount);
        }

        private const float BASE_SHARE = 0.55f;
        private const float MID_SHARE = 0.25f;
        private const float TOP_SHARE = 0.15f;
        private const float TOP2_SHARE = 0.05f;

        private static void ApplySweat(PlayerManager pm, float amount)
        {
            foreach (var region in Regions)
            {
                float overflow = 0f;

                overflow = AddWetnessUpTo(pm, region, ClothingLayer.Base, amount * BASE_SHARE + overflow, 80f);
                overflow = AddWetnessUpTo(pm, region, ClothingLayer.Mid, amount * MID_SHARE + overflow, 60f);
                overflow = AddWetnessUpTo(pm, region, ClothingLayer.Top, amount * TOP_SHARE + overflow, 40f);
                AddWetnessUpTo(pm, region, ClothingLayer.Top2, amount * TOP2_SHARE + overflow, 30f);
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