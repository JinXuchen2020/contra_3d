using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Contra3D.Core
{
    /// <summary>
    /// T-BDD-ADOPT-6cbd51: checkpoint_triggers_autosave — game state snapshot for save/load round-trip.
    /// Pure .NET data class (no UnityEngine) so the Core project compiles standalone.
    /// </summary>
    public class SaveData
    {
        [JsonConverter(typeof(Vector3Converter))]
        [JsonPropertyName("position")]
        public Vector3 Position { get; set; }

        [JsonPropertyName("health")]
        public float Health { get; set; }

        [JsonPropertyName("maxHealth")]
        public float MaxHealth { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("lives")]
        public int Lives { get; set; }

        /// <summary>
        /// Default save state: spawn position, full health, zero score, 3 lives.
        /// </summary>
        public static SaveData Default() =>
            new SaveData
            {
                Position = Vector3.Zero,
                Health = 100f,
                MaxHealth = 100f,
                Score = 0,
                Lives = 3,
            };
    }

    /// <summary>
    /// Minimal 3-D vector value type usable without UnityEngine.
    /// Mirrors UnityEngine.Vector3 operations required by AiSystem and ProjectileTypes.
    /// </summary>
    public struct Vector3 : IEquatable<Vector3>
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly Vector3 Zero = new Vector3(0f, 0f, 0f);
        public static readonly Vector3 One = new Vector3(1f, 1f, 1f);
        public static readonly Vector3 Up = new Vector3(0f, 1f, 0f);
        public static readonly Vector3 Down = new Vector3(0f, -1f, 0f);
        public static readonly Vector3 Forward = new Vector3(0f, 0f, 1f);
        public static readonly Vector3 Backward = new Vector3(0f, 0f, -1f);
        public static readonly Vector3 Left = new Vector3(-1f, 0f, 0f);
        public static readonly Vector3 Right = new Vector3(1f, 0f, 0f);
        public static readonly Vector3 UnitX = Right;
        public static readonly Vector3 UnitY = Up;
        public static readonly Vector3 UnitZ = Forward;

        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public float SqrMagnitude => X * X + Y * Y + Z * Z;

        public Vector3 Normalize() => this.normalized;

        private Vector3 normalized =>
            Magnitude == 0f ? Zero : new Vector3(X / Magnitude, Y / Magnitude, Z / Magnitude);

        public static Vector3 Normalize(Vector3 value) => value.Normalize();

        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Vector3(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) =>
            new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vector3 operator -(Vector3 a, Vector3 b) =>
            new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3 operator -(Vector3 a) =>
            new Vector3(-a.X, -a.Y, -a.Z);

        public static Vector3 operator *(Vector3 a, float d) =>
            new Vector3(a.X * d, a.Y * d, a.Z * d);

        public static Vector3 operator *(float d, Vector3 a) =>
            new Vector3(a.X * d, a.Y * d, a.Z * d);

        public static bool operator ==(Vector3 a, Vector3 b) =>
            a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        public static bool operator !=(Vector3 a, Vector3 b) =>
            !(a == b);

        public bool Equals(Vector3 other) =>
            X == other.X && Y == other.Y && Z == other.Z;

        public override bool Equals(object obj) =>
            obj is Vector3 other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(X, Y, Z);

        public override string ToString() =>
            $"({X}, {Y}, {Z})";
    }

    /// <summary>
    /// JSON converter for Vector3: serializes as { "x": ..., "y": ..., "z": ... }.
    /// </summary>
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for Vector3");

            float x = 0f, y = 0f, z = 0f;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected property name");

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "x": x = reader.GetSingle(); break;
                    case "y": y = reader.GetSingle(); break;
                    case "z": z = reader.GetSingle(); break;
                    default: throw new JsonException($"Unknown Vector3 property: {propertyName}");
                }
            }

            return new Vector3(x, y, z);
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteNumber("z", value.Z);
            writer.WriteEndObject();
        }
    }
}
