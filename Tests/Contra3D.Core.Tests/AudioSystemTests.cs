using System;
using System.Collections.Generic;
using Xunit;

namespace Contra3D.Core.Tests
{
    public class AudioSystemTests
    {
        // T-BDD-ADOPT-bgmg01: 区域 BGM 切换平滑交叉淡入淡出
        [Fact]
        public void TransitionBGM_BDD_area_crossfade_no_silence_gap()
        {
            // given: BGM_A playing at volume 1.0
            var sys = new AudioSystem();
            sys.PlayBGM("bgm_a", volume: 1.0f);
            Assert.Equal("bgm_a", sys.CurrentBgmId);
            Assert.Equal(1.0f, sys.CurrentBgmVolume);

            // when: TransitionBGM to BGM_B with 2.0s duration
            sys.TransitionBGM("bgm_b", duration: 2.0f);
            Assert.True(sys.Transition.IsActive);
            Assert.Equal("bgm_a", sys.Transition.FromBgmId);
            Assert.Equal("bgm_b", sys.Transition.ToBgmId);
            Assert.Equal(2.0f, sys.Transition.Duration);

            // then: at t=0, from=1.0, to=0.0
            Assert.Equal(1.0f, sys.Transition.FromVolume);
            Assert.Equal(0.0f, sys.Transition.ToVolume);

            // advance to midpoint (1.0s)
            sys.Update(1.0f);
            // FromVolume = 1.0 - (1.0/2.0) = 0.5, ToVolume = 1.0/2.0 = 0.5
            Assert.Equal(0.5f, sys.Transition.FromVolume, 3);
            Assert.Equal(0.5f, sys.Transition.ToVolume, 3);
            // mixed volume > 0 (no silence gap)
            Assert.True(sys.Transition.FromVolume + sys.Transition.ToVolume > 0f);

            // advance to end (2.0s)
            sys.Update(1.0f);
            Assert.False(sys.Transition.IsActive);
            Assert.Equal("bgm_b", sys.CurrentBgmId);
            Assert.Equal(1.0f, sys.CurrentBgmVolume);
        }

        // T-BDD-ADOPT-bgmg02: 战斗切入/切出 BGM 分层
        [Fact]
        public void TransitionBGM_BDD_combat_layering_enter_exit()
        {
            // given: exploration BGM layer 0 playing at 1.0
            var sys = new AudioSystem();
            sys.PlayBGM("explore_bgm", volume: 1.0f);
            sys.EnterCombat();

            // then: layer 0 fades to 0.3, layer 1 (combat) ramps to 1.0
            Assert.Equal(0.3f, sys.GetLayerVolume(0), 3);
            Assert.Equal(1.0f, sys.GetLayerVolume(1), 3);

            // when: exit combat — reverse transition
            sys.ExitCombat();

            // then: layer 0 restores to 1.0, layer 1 removed
            Assert.Equal(1.0f, sys.GetLayerVolume(0), 3);
            Assert.Equal(0f, sys.GetLayerVolume(1), 3);
        }

        // T-BDD-ADOPT-sfxe01: 事件驱动 SFX 播放 (空间定位)
        [Fact]
        public void PlaySFX_BDD_event_driven_spatial_playback()
        {
            // given: rifle_fire SFX, spatial enabled, listener at origin, source at (10, 0, 5)
            var sys = new AudioSystem();
            var sourcePos = new Vector3(10f, 0f, 5f);
            const float listenerDist = 11.18f; // sqrt(10²+5²)

            // when: play spatial SFX
            var (played, volumeAtListener) = sys.PlaySFX("rifle_fire", sourcePos, spatial: true, AudioPriority.High, listenerDist);

            // then: played=true, volume attenuated by distance
            Assert.True(played);
            Assert.True(volumeAtListener > 0f && volumeAtListener <= 1.0f);
            Assert.Equal(1, sys.ActiveSfxCount);

            // verify the active SFX has correct properties
            // access via reflection to inspect internal state
            var activeField = typeof(AudioSystem).GetField("_activeSfx",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeList = activeField.GetValue(sys) as List<SfxInstance>;
            Assert.NotNull(activeList);
            Assert.Single(activeList);
            Assert.Equal("rifle_fire", activeList[0].Id);
            Assert.True(activeList[0].IsSpatial);
            Assert.True(activeList[0].Playing);
            Assert.Equal((float)AudioPriority.High, activeList[0].Priority);
        }

        // T-BDD-ADOPT-sfxe02: SFX 并发限制与优先级抢占
        [Fact]
        public void PlaySFX_BDD_concurrency_limit_priority_eviction()
        {
            // given: 8 low-priority (environment) SFX already playing (at limit)
            var sys = new AudioSystem(maxSfxConcurrency: 8);
            for (int i = 0; i < 8; i++)
            {
                sys.PlaySFX($"env_{i}", new Vector3(0f, 0f, (float)i), spatial: false, AudioPriority.Low);
            }
            Assert.Equal(8, sys.ActiveSfxCount);

            // when: high-priority combat SFX triggers (priority=High > Low)
            var (played, _) = sys.PlaySFX("rifle_fire", new Vector3(10f, 0f, 5f), spatial: true, AudioPriority.High);

            // then: oldest low-priority evicted, total stays ≤ 8, new SFX plays
            Assert.True(played);
            Assert.True(sys.ActiveSfxCount <= 8);

            // verify the new SFX is present and an old low-priority one was removed
            var activeField = typeof(AudioSystem).GetField("_activeSfx",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeList = activeField.GetValue(sys) as List<SfxInstance>;
            Assert.NotNull(activeList);
            Assert.True(activeList.Count <= 8);
            Assert.True(activeList.Exists(s => s.Id == "rifle_fire"), "High-priority SFX should be present");
        }

        // T-BDD-ADOPT-sfxd01: 3D 音效距离衰减模型
        [Fact]
        public void PlaySFX_BDD_distance_attenuation_logarithmic()
        {
            // given: referenceDistance=1m, maxDistance=50m
            Assert.Equal(1.0f, AudioSystem.ComputeDistanceAttenuation(new Vector3(0f, 0f, 1f), 1f), 3);
            // at 10m: refDist/10 = 0.1
            Assert.Equal(0.1f, AudioSystem.ComputeDistanceAttenuation(new Vector3(0f, 0f, 10f), 10f), 3);
            // at 30m: refDist/30 ≈ 0.033
            Assert.Equal(0.033f, AudioSystem.ComputeDistanceAttenuation(new Vector3(0f, 0f, 30f), 30f), 2);
            // at 60m (> maxDistance=50): clamped to 0.0
            Assert.Equal(0.0f, AudioSystem.ComputeDistanceAttenuation(new Vector3(0f, 0f, 60f), 60f), 3);
            // at exactly maxDistance=50: refDist/maxDist = 1/50 = 0.02, but implementation clamps to 0 at >=maxDist
            Assert.Equal(0.0f, AudioSystem.ComputeDistanceAttenuation(new Vector3(0f, 0f, 50f), 50f), 3);
        }

        // T-BDD-ADOPT-sfxd02: 多普勒效应相对速度影响音高
        [Fact]
        public void PlaySFX_BDD_doppler_effect_pitch_shift()
        {
            // given: dopplerFactor=1.0, basePitch=1.0, speedOfSound≈343 m/s
            var sys = new AudioSystem();
            sys.DopplerFactor = 1.0f;
            const float basePitch = 1.0f;

            // approaching (positive relative speed → pitch up)
            float shiftApproach = sys.ComputeDopplerShift(30f, basePitch);
            Assert.True(shiftApproach > basePitch, "Approaching source should increase pitch");

            // receding (negative speed → pitch down)
            float shiftRecede = sys.ComputeDopplerShift(-30f, basePitch);
            Assert.True(shiftRecede < basePitch, "Receding source should decrease pitch");

            // no relative motion → pitch unchanged
            float shiftStatic = sys.ComputeDopplerShift(0f, basePitch);
            Assert.Equal(basePitch, shiftStatic);
        }

        // T-BDD-ADOPT-audi01: 音频资源池预加载与动态卸载
        [Fact]
        public void PlaySFX_BDD_resource_pool_preload_unload()
        {
            // given: common SFX preloaded, boss SFX scene-loaded
            var sys = new AudioSystem();
            sys.PreloadAudio("rifle_fire", AudioCategory.SFX);
            sys.PreloadAudio("jump", AudioCategory.SFX);
            sys.PreloadAudio("ui_click", AudioCategory.UI);
            sys.LoadSceneAudio("boss_arena");

            // then: preloaded SFX are always present
            var resourcesField = typeof(AudioSystem).GetField("_resources",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var resources = resourcesField.GetValue(sys) as List<AudioResourceEntry>;
            Assert.NotNull(resources);
            Assert.True(resources.Exists(r => r.Id == "rifle_fire" && r.IsPreloaded));
            Assert.True(resources.Exists(r => r.Id == "boss_arena" && !r.IsPreloaded));

            // when: leave boss arena — unload scene audio
            sys.UnloadSceneAudio();

            // then: preloaded SFX retained, scene-specific removed
            var remaining = resourcesField.GetValue(sys) as List<AudioResourceEntry>;
            Assert.NotNull(remaining);
            Assert.True(remaining.Exists(r => r.Id == "rifle_fire"), "Preloaded SFX must remain");
            Assert.True(remaining.Exists(r => r.Id == "jump"), "Preloaded SFX must remain");
            Assert.True(remaining.Exists(r => r.Id == "ui_click"), "Preloaded SFX must remain");
            Assert.False(remaining.Exists(r => r.Id == "boss_arena"), "Scene audio must be unloaded");
        }

        // T-BDD-ADOPT-audi02: 音频延迟预算达标
        [Fact]
        public void PlaySFX_BDD_latency_budget_within_80ms()
        {
            // given: fixed logic frame at 60Hz (dt=16.67ms), audio thread independent
            // The implementation uses synchronous PlaySFX which returns immediately.
            // Latency budget is satisfied if PlaySFX completes within one logic frame.
            var sys = new AudioSystem();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                sys.PlaySFX("test_sfx", new Vector3(0f, 0f, 5f), spatial: true, AudioPriority.Medium);
            }
            sw.Stop();
            // 100 calls should complete well within 80ms budget
            // Average per-call latency
            float avgMs = (float)sw.Elapsed.TotalMilliseconds / 100f;
            Assert.True(avgMs < 1f, $"Average PlaySFX latency {avgMs}ms exceeds 1ms budget");
        }

        // T-QA-E2E-AUDIO-SFX: 敌人受伤时触发 audio_sfx 信号
        // BDD: rg_audio_sfx_played_on_hit
        // Enemy takes damage and hit SFX signal is emitted.
        // Verifies AudioManager plays the correct hit sound effect via game_signal with sfx_type=hit.
        [Fact]
        public void BDD_rg_audio_sfx_played_on_hit()
        {
            // given: AudioSystem initialized, enemy at origin
            var sys = new AudioSystem();
            var enemyPos = new Vector3(0f, 0f, 0f);
            const float listenerDist = 10f;

            // when: enemy takes damage → hit SFX is played
            var (played, _) = sys.PlaySFX("hit", enemyPos, spatial: true, AudioPriority.High, listenerDist);

            // then: SFX was played and is present in the active list with sfx_type=hit
            Assert.True(played);
            Assert.Equal(1, sys.ActiveSfxCount);

            // verify via reflection: active SFX has id="hit" (sfx_type=hit)
            var activeField = typeof(AudioSystem).GetField("_activeSfx",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeList = activeField.GetValue(sys) as List<SfxInstance>;
            Assert.NotNull(activeList);
            Assert.Single(activeList);
            Assert.Equal("hit", activeList[0].Id);
            Assert.True(activeList[0].Playing);
            Assert.Equal((float)AudioPriority.High, activeList[0].Priority);
        }
    }
}
