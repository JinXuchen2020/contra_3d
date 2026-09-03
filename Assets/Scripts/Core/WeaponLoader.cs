using System;
using System.Collections.Generic;
using System.IO;

namespace Contra3D.Core
{
    /// <summary>
    /// 从 YAML 文件加载武器定义。使用简单文本解析（不引入新依赖）。
    /// 格式：YAML 列表，每个条目含 weapon_id/name/type/damage/fire_rate/magazine_size/reload_time/spread。
    /// </summary>
    public static class WeaponLoader
    {
        /// <summary>从文件加载武器字典。</summary>
        public static Dictionary<string, WeaponDefinition> LoadFromFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Weapons YAML not found: {path}");

            var weapons = new Dictionary<string, WeaponDefinition>();
            string[] lines = File.ReadAllLines(path);
            var current = new Dictionary<string, string>();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("- weapon_id:"))
                {
                    // Save previous
                    if (current.Count > 0)
                    {
                        var def = ParseWeapon(current);
                        if (def != null) weapons[def.Id] = def;
                    }
                    current = new Dictionary<string, string>();
                    string val = trimmed.Substring("- weapon_id:".Length).Trim();
                    current["weapon_id"] = val.Trim('"').Trim('\'');
                }
                else if (trimmed.StartsWith("name:"))
                    current["name"] = trimmed.Substring("name:".Length).Trim().Trim('"').Trim('\'');
                else if (trimmed.StartsWith("type:"))
                    current["type"] = trimmed.Substring("type:".Length).Trim().ToLower();
                else if (trimmed.StartsWith("damage:"))
                    current["damage"] = trimmed.Substring("damage:".Length).Trim();
                else if (trimmed.StartsWith("fire_rate:"))
                    current["fire_rate"] = trimmed.Substring("fire_rate:".Length).Trim();
                else if (trimmed.StartsWith("magazine_size:"))
                    current["magazine_size"] = trimmed.Substring("magazine_size:".Length).Trim();
                else if (trimmed.StartsWith("reload_time:"))
                    current["reload_time"] = trimmed.Substring("reload_time:".Length).Trim();
                else if (trimmed.StartsWith("spread:"))
                    current["spread"] = trimmed.Substring("spread:".Length).Trim();
            }

            // Last entry
            if (current.Count > 0)
            {
                var def = ParseWeapon(current);
                if (def != null) weapons[def.Id] = def;
            }

            return weapons;
        }

        private static WeaponDefinition ParseWeapon(Dictionary<string, string> fields)
        {
            if (!fields.TryGetValue("weapon_id", out var id) || string.IsNullOrEmpty(id))
                return null;
            if (!fields.TryGetValue("name", out var name)) name = id;
            if (!fields.TryGetValue("type", out var typeStr)) typeStr = "hitscan";
            if (!float.TryParse(fields.TryGetValue("damage", out var dmg) ? dmg : "1", out var damage)) damage = 1f;
            if (!float.TryParse(fields.TryGetValue("fire_rate", out var fr) ? fr : "1", out var fireRate)) fireRate = 1f;
            if (!int.TryParse(fields.TryGetValue("magazine_size", out var mag) ? mag : "30", out var magazine)) magazine = 30;
            if (!float.TryParse(fields.TryGetValue("reload_time", out var rt) ? rt : "0", out var reloadTime)) reloadTime = 0f;
            if (!float.TryParse(fields.TryGetValue("spread", out var sp) ? sp : "0", out var spread)) spread = 0f;

            WeaponType type = typeStr switch
            {
                "projectile" => WeaponType.Projectile,
                "melee" => WeaponType.Melee,
                _ => WeaponType.Hitscan
            };

            return new WeaponDefinition(id, name, type, damage, fireRate, magazine, reloadTime, spread);
        }
    }
}
