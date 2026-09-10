using System;
using System.Collections.Generic;

namespace Contra3D.Core
{
    public enum AudioCategory { BGM, SFX, UI }
    public enum AudioPriority { Low = 1, Medium = 2, High = 3 }

    public struct BgmTransitionState
    {
        public string FromBgmId; public string ToBgmId;
        public float Duration; public float Elapsed; public bool IsActive;
        public float FromVolume => IsActive ? 1.0f - (Elapsed / Duration) : 1.0f;
        public float ToVolume   => IsActive ? (Elapsed / Duration) : 0.0f;
        public bool IsComplete  => IsActive && Elapsed >= Duration;
    }

    public struct SfxInstance
    {
        public string Id; public float Volume; public bool IsSpatial;
        public Vector3 Position; public float Priority; public bool Playing; public float Pan;
    }

    public struct AudioResourceEntry
    {
        public string Id; public AudioCategory Category;
        public bool IsPreloaded; public bool IsLoaded;
    }

    public class AudioSystem
    {
        private string _currentBgmId;
        private float _currentBgmVolume;
        private readonly Dictionary<int, float> _layerVolumes;
        private BgmTransitionState _transition;
        private readonly List<AudioResourceEntry> _resources;
        public int MaxSfxConcurrency { get; private set; } = 8;
        private readonly List<SfxInstance> _activeSfx;
        private float _dopplerFactor = 1.0f;
        public float DopplerFactor { get => _dopplerFactor; set => _dopplerFactor = value; }

        public string CurrentBgmId => _currentBgmId;
        public float CurrentBgmVolume => _currentBgmVolume;
        public BgmTransitionState Transition => _transition;
        public int ActiveSfxCount => _activeSfx.Count;
        public float GetLayerVolume(int layer) => _layerVolumes.TryGetValue(layer, out var v) ? v : 0f;

        public AudioSystem(int maxSfxConcurrency = 8)
        {
            _currentBgmId = null; _currentBgmVolume = 0f;
            _layerVolumes = new Dictionary<int, float>();
            _transition = new BgmTransitionState { IsActive = false };
            _resources = new List<AudioResourceEntry>();
            _activeSfx = new List<SfxInstance>();
            MaxSfxConcurrency = maxSfxConcurrency;
        }

        public void PlayBGM(string bgmId, float volume = 1.0f)
        {
            if (_currentBgmId != null && _currentBgmId != bgmId) { _currentBgmId = null; _currentBgmVolume = 0f; }
            _currentBgmId = bgmId; _currentBgmVolume = volume;
            EnsureLoaded(bgmId);
        }

        public void TransitionBGM(string newBgmId, float duration = 2.0f)
        {
            if (_currentBgmId == newBgmId) return;
            _transition = new BgmTransitionState { FromBgmId = _currentBgmId, ToBgmId = newBgmId, Duration = duration, Elapsed = 0f, IsActive = true };
            EnsureLoaded(newBgmId);
        }

        public void EnterCombat() { _layerVolumes[0] = 0.3f; _layerVolumes[1] = 1.0f; }
        public void ExitCombat() { _layerVolumes[0] = 1.0f; _layerVolumes.Remove(1); }

        public (bool Played, float VolumeAtListener) PlaySFX(string sfxId, Vector3 position, bool spatial = false, AudioPriority priority = AudioPriority.Medium, float listenerDistance = 10f)
        {
            float volume = spatial ? ComputeDistanceAttenuation(position, listenerDistance) : 1.0f;
            float pan = ComputePan(position, listenerDistance);
            if (_activeSfx.Count >= MaxSfxConcurrency) EvictLowest();
            _activeSfx.Add(new SfxInstance { Id = sfxId, Volume = volume, IsSpatial = spatial, Position = position, Priority = (float)priority, Playing = true, Pan = pan });
            return (true, volume);
        }

        public static float ComputeDistanceAttenuation(Vector3 sourcePos, float listenerDistance)
        {
            const float refDist = 1.0f, maxDist = 50.0f;
            if (listenerDistance <= 0f) return 1.0f;
            if (listenerDistance >= maxDist) return 0.0f;
            return Math.Max(0f, Math.Min(1f, refDist / listenerDistance));
        }

        public static float ComputePan(Vector3 sourcePos, float distance)
        {
            float panX = sourcePos.X / Math.Max(distance, 1f);
            return Math.Max(-1f, Math.Min(1f, panX));
        }

        public float ComputeDopplerShift(float relativeSpeedMps, float basePitch = 1.0f)
        {
            const float speedOfSound = 343f;
            if (relativeSpeedMps == 0f) return basePitch;
            return basePitch * (1f + relativeSpeedMps / speedOfSound * _dopplerFactor);
        }

        public void PreloadAudio(string id, AudioCategory category = AudioCategory.SFX)
        { _resources.Add(new AudioResourceEntry { Id = id, Category = category, IsPreloaded = true, IsLoaded = true }); }

        private bool _isAsyncLoading;
        public bool IsAsyncLoading => _isAsyncLoading;

        public void LoadSceneAudio(string sceneId)
        {
            _isAsyncLoading = true;
            _resources.Add(new AudioResourceEntry { Id = sceneId, Category = AudioCategory.BGM, IsPreloaded = false, IsLoaded = true });
            _isAsyncLoading = false;
        }

        public void UnloadSceneAudio() { _resources.RemoveAll(r => !r.IsPreloaded); }

        public void Update(float dt)
        {
            if (_transition.IsActive)
            {
                _transition.Elapsed += dt;
                if (_transition.IsComplete)
                {
                    _currentBgmId = _transition.ToBgmId;
                    _currentBgmVolume = 1.0f;
                    _transition.IsActive = false;
                    _layerVolumes.Clear();
                }
            }
        }

        private void EnsureLoaded(string bgmId)
        {
            if (!_resources.Exists(r => r.Id == bgmId))
                _resources.Add(new AudioResourceEntry { Id = bgmId, Category = AudioCategory.BGM, IsPreloaded = false, IsLoaded = true });
        }

        private void EvictLowest()
        {
            if (_activeSfx.Count == 0) return;
            int idx = 0; float lowest = _activeSfx[0].Priority;
            for (int i = 1; i < _activeSfx.Count; i++)
                if (_activeSfx[i].Priority < lowest) { lowest = _activeSfx[i].Priority; idx = i; }
            _activeSfx.RemoveAt(idx);
        }
    }
}
