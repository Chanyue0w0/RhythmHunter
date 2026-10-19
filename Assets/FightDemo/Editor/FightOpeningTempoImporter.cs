using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FMODUnity;
using RhythmHunter.RhythmDemo;
using RhythmHunter.FightDemo;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RhythmHunter.FightDemoEditor
{
    [InitializeOnLoad]
    public sealed class FightOpeningTempoImporter : IProcessSceneWithReport
    {
        static FightOpeningTempoImporter()
        {
            FmodBeatClock.EditorOpeningTempoResolver = ReadTempo;
        }

        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var clock in root.GetComponentsInChildren<FmodBeatClock>(true))
                {
                    var fight = clock.GetComponent<FightCombatController>();
                    if (fight == null || !fight.AutoPlayBattleEntrance) continue;
                    float tempo = ReadTempo(clock.MusicEventPath);
                    if (tempo <= 0 && report != null)
                        throw new BuildFailedException("Cannot bake opening tempo for " + clock.MusicEventPath);
                    clock.ConfigureOpeningTempo(tempo);
                }
        }

        public static float ReadTempo(string eventPath)
        {
            try
            {
                var entry = EventManager.EventFromPath(eventPath);
                if (entry == null) throw new InvalidOperationException("FMOD event is not in the event cache: " + eventPath);
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../ProjectFMOD"));
                string filename = "{" + entry.Guid.ToString().Trim('{', '}') + "}.xml";
                string[] files = Directory.GetFiles(root, filename, SearchOption.AllDirectories);
                if (files.Length != 1) throw new InvalidOperationException("Expected one FMOD event metadata file: " + filename);
                var marker = XDocument.Load(files[0]).Descendants("object")
                    .Where(x => (string)x.Attribute("class") == "TempoMarker")
                    .OrderBy(x => Value(x, "position", 0)).FirstOrDefault();
                float tempo = marker != null ? Value(marker, "tempo", 0) : 0;
                if (tempo <= 0) throw new InvalidOperationException("Event has no valid Tempo Marker: " + eventPath);
                return tempo;
            }
            catch (Exception error)
            {
                Debug.LogError("[FightOpeningTempo] " + error.Message);
                return 0;
            }
        }

        private static float Value(XElement element, string property, float fallback)
        {
            string text = element.Elements("property").FirstOrDefault(x => (string)x.Attribute("name") == property)
                ?.Element("value")?.Value;
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }
    }
}
