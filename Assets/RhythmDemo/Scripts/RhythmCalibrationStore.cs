using System;
using System.IO;
using UnityEngine;

namespace RhythmHunter.RhythmDemo
{
    /// <summary>Shared by calibration and combat scenes. Legacy preferences are migrated without deletion.</summary>
    public static class RhythmCalibrationStore
    {
        [Serializable]
        public sealed class Data
        {
            public int version = 1;
            public bool keyboardSaved;
            public float keyboardDelayMs;
            public bool gamepadSaved;
            public float gamepadDelayMs;
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, "rhythm-calibration.json");
        public static string LastError { get; private set; } = "";
        static Data cached;
        static bool writable = true;

        public static Data Read()
        {
            EnsureLoaded();
            return Copy(cached);
        }

        public static bool HasSaved(string profile)
        {
            EnsureLoaded();
            return profile == "Gamepad" ? cached.gamepadSaved : cached.keyboardSaved;
        }

        public static float GetDelay(string profile)
        {
            EnsureLoaded();
            return profile == "Gamepad" ? cached.gamepadDelayMs : cached.keyboardDelayMs;
        }

        public static bool Save(string profile, float delay)
        {
            EnsureLoaded();
            if (!Valid(delay) || !writable) return false;
            Data next = Copy(cached);
            if (profile == "Gamepad") { next.gamepadSaved = true; next.gamepadDelayMs = delay; }
            else { next.keyboardSaved = true; next.keyboardDelayMs = delay; }
            if (!Write(next)) return false;
            cached = next;
            // Keep the old format intact for compatibility with earlier versions.
            PlayerPrefs.SetFloat("FightTiming.v1." + (profile == "Gamepad" ? "Gamepad" : "Keyboard"), delay);
            PlayerPrefs.Save();
            return true;
        }

        static void EnsureLoaded()
        {
            if (cached != null) return;
            var legacy = new Data
            {
                keyboardSaved = PlayerPrefs.HasKey("FightTiming.v1.Keyboard"),
                keyboardDelayMs = PlayerPrefs.GetFloat("FightTiming.v1.Keyboard", 0),
                gamepadSaved = PlayerPrefs.HasKey("FightTiming.v1.Gamepad"),
                gamepadDelayMs = PlayerPrefs.GetFloat("FightTiming.v1.Gamepad", 0)
            };
            cached = legacy;
            if (File.Exists(FilePath))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
                    if (loaded == null || loaded.version != 1 || !Valid(loaded.keyboardDelayMs) || !Valid(loaded.gamepadDelayMs))
                        throw new InvalidDataException("Unsupported or invalid calibration file.");
                    cached = loaded;
                }
                catch (Exception e)
                {
                    // Preserve the problematic file and fall back to the original values.
                    writable = false;
                    LastError = "Calibration file preserved; using legacy preferences. " + e.Message;
                    Debug.LogWarning(LastError);
                }
            }
            else if (Valid(legacy.keyboardDelayMs) && Valid(legacy.gamepadDelayMs) && Write(legacy))
            {
                try
                {
                    string backup = Path.Combine(Application.persistentDataPath, "rhythm-calibration.legacy-backup.json");
                    if (!File.Exists(backup)) File.Copy(FilePath, backup);
                }
                catch (Exception e) { Debug.LogWarning("Could not create the legacy calibration backup: " + e.Message); }
            }
        }

        static bool Write(Data data)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                else File.Move(temp, FilePath);
                LastError = "";
                return true;
            }
            catch (Exception e)
            {
                LastError = "Could not save calibration: " + e.Message;
                Debug.LogWarning(LastError);
                return false;
            }
        }

        static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= FmodRhythmJudge.MaxPersonalDelayMs;
        static Data Copy(Data data) => new Data { version = data.version, keyboardSaved = data.keyboardSaved,
            keyboardDelayMs = data.keyboardDelayMs, gamepadSaved = data.gamepadSaved, gamepadDelayMs = data.gamepadDelayMs };
    }
}
