using System;
using System.Collections.Generic;
using System.Numerics;

namespace Contra3D.Core
{
    /// <summary>鏁屼汉 AI 绫诲瀷銆?/summary>
    public enum AiType
    {
        Patrol,
        Chase,
        Sniper,
        Rusher,
    }

    /// <summary>鏁屼汉 AI 鐘舵€併€?/summary>
    public enum AiState
    {
        Idle,
        Patrol,
        Alert,
        Combat,
        Chase,
        Aim,
        Rush,
        Staggered,
        Dead,
    }

    /// <summary>鏁屼汉瀹氫箟锛堜笉鍙彉锛夈€?/summary>
    public sealed class EnemyDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public float Health { get; }
        public float Speed { get; }
        public AiType AiType { get; }
        public float VisionRange { get; }
        public float VisionAngleDeg { get; }
        public float AttackRange { get; }
        public float AlertThreshold { get; }
        public float ComprehensionThreshold { get; }
        public float VigilanceGainPerSecond { get; }
        public float VigilanceDecayPerSecond { get; }
        public float SoundVigilanceGain { get; }
        public float HitVigilanceInstant { get; }
        public float Score { get; }
        /// <summary>鏁屼汉鍙敤鐨勬鍣?ID 鍒楄〃锛堟潵鑷?enemies.yaml 鐨?weapons 瀛楁锛夈€傜┖鍒楄〃琛ㄧず鏃犺繙绋嬫鍣ㄣ€?/summary>
        public IReadOnlyList<string> Weapons { get; }

        public EnemyDefinition(
            string id, string name, float health, float speed, AiType aiType,
            float visionRange = 15f, float visionAngleDeg = 90f, float attackRange = 5f,
            float alertThreshold = 60f, float comprehensionThreshold = 100f,
            float vigilanceGainPerSecond = 20f, float vigilanceDecayPerSecond = 10f,
            float soundVigilanceGain = 35f, float hitVigilanceInstant = 100f,
            float score = 100f,
            IReadOnlyList<string> weapons = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id must not be null or whitespace.", nameof(id));
            if (health <= 0f) throw new ArgumentException($"Health must be > 0, got {health}.", nameof(health));
            if (speed < 0f) throw new ArgumentException($"Speed must be >= 0, got {speed}.", nameof(speed));
            Id = id;
            Name = name;
            Health = health;
            Speed = speed;
            AiType = aiType;
            VisionRange = visionRange;
            VisionAngleDeg = visionAngleDeg;
            AttackRange = attackRange;
            AlertThreshold = alertThreshold;
            ComprehensionThreshold = comprehensionThreshold;
            VigilanceGainPerSecond = vigilanceGainPerSecond;
            VigilanceDecayPerSecond = vigilanceDecayPerSecond;
            SoundVigilanceGain = soundVigilanceGain;
            HitVigilanceInstant = hitVigilanceInstant;
            Score = score;
            Weapons = weapons ?? Array.Empty<string>();
        }

        /// <summary>
        /// 鏋勫缓鍣ㄦā寮?鈥?绠€鍖栨祴璇曚笌鏁版嵁椹卞姩鍒涘缓銆?        /// </summary>
        public class Builder
        {
            private string _id;
            private string _name;
            private float _health = 24f;
            private float _speed = 2f;
            private Contra3D.Core.AiType _aiType = Contra3D.Core.AiType.Patrol;
            private float _visionRange = 15f;
            private float _attackRange = 5f;
            private float _alertThreshold = 60f;
            private float _comprehensionThreshold = 100f;
            private IReadOnlyList<string> _weapons;

            public Builder Id(string id) { _id = id; return this; }
            public Builder Name(string name) { _name = name; return this; }
            public Builder Health(float h) { _health = h; return this; }
            public Builder Speed(float s) { _speed = s; return this; }
            public Builder AiType(AiType t) { _aiType = t; return this; }
            public Builder VisionRange(float v) { _visionRange = v; return this; }
            public Builder AttackRange(float a) { _attackRange = a; return this; }
            public Builder AlertThreshold(float t) { _alertThreshold = t; return this; }
            public Builder ComprehensionThreshold(float t) { _comprehensionThreshold = t; return this; }
            public Builder Weapons(IReadOnlyList<string> w) { _weapons = w; return this; }

            public EnemyDefinition Build()
            {
                return new EnemyDefinition(_id, _name, _health, _speed, _aiType,
                    visionRange: _visionRange, attackRange: _attackRange,
                    alertThreshold: _alertThreshold, comprehensionThreshold: _comprehensionThreshold,
                    weapons: _weapons);
            }
        }

        public static Builder CreateBuilder(string id, string name) => new Builder().Id(id).Name(name);
    }

    /// <summary>鏁屼汉 AI 鐘舵€侊紙鍙彉锛岀敱 AiSystem 绠＄悊锛夈€?/summary>
    public class EnemyAIState
    {
        public string EnemyId { get; set; }
        public Vector3 Position { get; set; }
        public AiType AiType { get; set; }
        public AiState State { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float Vigilance { get; set; }
        public float TimeSinceLastStimulus { get; set; }
        public AiState StaggerPrevState { get; set; }
        public Vector3 PatrolTarget { get; set; }
        public bool IsAlive => State != AiState.Dead && Health > 0f;

        public void Reset(string enemyId, EnemyDefinition def, Vector3 startPosition)
        {
            EnemyId = enemyId;
            Position = startPosition;
            AiType = def.AiType;
            State = AiState.Idle;
            Health = def.Health;
            MaxHealth = def.Health;
            Vigilance = 0f;
            TimeSinceLastStimulus = 0f;
            PatrolTarget = startPosition;
        }
    }

    /// <summary>AI 杈撳嚭鎸囦护锛堟瘡甯х敱鐘舵€佹満浜у嚭锛夈€?/summary>
    public struct AICommand
    {
        public Vector3 MoveIntent;
        public bool FireRequest;
        public string TargetId;
        /// <summary>鏁屼汉浣跨敤鐨勬鍣?ID锛堟潵鑷?EnemyDefinition.Weapons锛夈€傜┖瀛楃涓茶〃绀烘棤姝﹀櫒銆?/summary>
        public string WeaponId;

        public static AICommand Idle => new AICommand { MoveIntent = Vector3.Zero, FireRequest = false };
        public static AICommand Move(Vector3 dir) => new AICommand { MoveIntent = dir, FireRequest = false };
        public static AICommand Attack(string targetId, string weaponId = "") => new AICommand { MoveIntent = Vector3.Zero, FireRequest = true, TargetId = targetId, WeaponId = weaponId };
    }

    /// <summary>鏁屼汉灏勫嚮璇锋眰锛堢敱 AiSystem 浜у嚭锛屼緵 CombatSystem 娑堣垂锛夈€?/summary>
    public readonly struct EnemyFireRequest
    {
        public string EnemyId { get; }
        public string WeaponId { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }

        public EnemyFireRequest(string enemyId, string weaponId, Vector3 origin, Vector3 direction)
        {
            EnemyId = enemyId ?? throw new ArgumentException("EnemyId must not be null.");
            WeaponId = weaponId ?? throw new ArgumentException("WeaponId must not be null.");
            Origin = origin;
            Direction = direction;
        }
    }


}