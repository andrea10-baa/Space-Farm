using System;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace SpaceFarm.Editor
{
    /// <summary>Idempotently imports the bilingual source file into Unity Localization tables.</summary>
    public static class SpaceFarmLocalizationSetup
    {
        private const string Folder = "Assets/Localization";
        private const string TableName = "SpaceFarm UI";

        [MenuItem("Space Farm/Update Localization Tables")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Localization");
            LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Folder + "/Localization Settings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            Locale spanish = EnsureLocale("es");
            Locale english = EnsureLocale("en");
            LocalizationSettings.ProjectLocale = spanish;
            foreach (IStartupLocaleSelector selector in LocalizationSettings.StartupLocaleSelectors)
                if (selector is SpecificLocaleSelector specific) specific.LocaleId = spanish.Identifier;
            EditorUtility.SetDirty(settings);

            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName)
                ?? LocalizationEditorSettings.CreateStringTableCollection(TableName, Folder);
            StringTable es = collection.GetTable(spanish.Identifier) as StringTable;
            StringTable en = collection.GetTable(english.Identifier) as StringTable;
            if (es == null || en == null) throw new Exception("Space Farm: localization tables were not created.");

            int count = 0;
            foreach (string line in File.ReadAllLines("Assets/Editor/SpaceFarmTranslations.tsv"))
            {
                if (string.IsNullOrWhiteSpace(line) || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0) throw new Exception("Invalid translation line: " + line);
                string source = line.Substring(0, tab).Replace("\\n", "\n");
                string target = line.Substring(tab + 1).Replace("\\n", "\n");
                if (string.IsNullOrWhiteSpace(target)) throw new Exception("Empty English translation for " + source);
                (es.GetEntry(source) ?? es.AddEntry(source, source)).Value = source;
                (en.GetEntry(source) ?? en.AddEntry(source, target)).Value = target;
                count++;
            }
            EditorUtility.SetDirty(es);
            EditorUtility.SetDirty(en);
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            Debug.Log("SPACE_FARM_LOCALIZATION_READY: " + count + " keys in es/en");
        }

        private static Locale EnsureLocale(string code)
        {
            Locale locale = LocalizationEditorSettings.GetLocale(code);
            if (locale != null) return locale;
            locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, Folder + "/" + code + ".asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }
    }
}
