using TMPro;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SpaceFarm
{
    /// <summary>Reads the Space Farm string tables; the package's SelectedLocale is the only language state.</summary>
    public static class SpaceFarmLocalization
    {
        private const string LanguagePreference = "SpaceFarm.Language";
        private const string TableName = "SpaceFarm UI";
        private static StringTable fragmentTable;
        private static readonly Regex numberPattern = new Regex(@"(?<![\p{L}\d])\d+(?:[.,]\d+)?(?![\p{L}\d])", RegexOptions.CultureInvariant);
        private static readonly List<KeyValuePair<Regex, string>> fragments = new List<KeyValuePair<Regex, string>>();
        private static readonly string[] FragmentKeys =
        {
            "MEJORAR ESTRUCTURA", "MEJORAR ROVER", "REPARAR ROVER", "REPARACIÓN EN COLA",
            "RECOGER HIERRO", "RECOGER OXÍGENO", "RECOGER COMIDA", "RECOGER AGUA",
            "Depósito local", "Carga del rover", "Reserva utilizable", "Capacidad compartida",
            "Averiada por tormenta", "Sin producción", "Almacén lleno", "Sin trabajador",
            "Astronautas libres", "energía personal", "en construcción", "Mejora en curso",
            "ENVIAR ROVER", "REPARACIÓN", "REPARAR", "MEJORAR", "CONSTRUIR",
            "NIVEL", "CULTIVO", "COMIDA", "SOL", "Nivel", "nivel", "sectores", "sector", "de la base", "hacia el", "hacia la",
            "producción", "almacenado", "almacenada", "capacidad", "unidades de juego",
            "unidades", "unidad", "unid.", "soles", "sol", "real", "último", "trabajando",
            "descansando", "comida", "agua", "hierro", "energía", "oxígeno",
            "Mina", "Extractor de agua", "Planta de oxígeno", "Invernadero", "Almacén",
            "Colonia", "cultivo", "Norte", "Sur", "Este", "Oeste", "norte", "sur", "este", "oeste"
        };

        public static bool IsEnglish => LocalizationSettings.SelectedLocale != null &&
                                        LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("en");

        public static void RestorePreference()
        {
            SetLanguage(PlayerPrefs.GetString(LanguagePreference, "es"), false);
        }

        public static void SetLanguage(string code, bool persist = true)
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code == "en" ? "en" : "es");
            if (locale == null)
            {
                Debug.LogWarning("Space Farm: missing locale " + code);
                return;
            }
            LocalizationSettings.SelectedLocale = locale;
            if (!persist) return;
            PlayerPrefs.SetString(LanguagePreference, locale.Identifier.Code);
            PlayerPrefs.Save();
        }

        public static string T(string spanish)
        {
            if (string.IsNullOrEmpty(spanish)) return spanish;
            StringTable table = ReadyTable();
            StringTableEntry entry = table != null ? table.GetEntry(spanish) : null;
            if (entry != null && !string.IsNullOrEmpty(entry.Value)) return entry.Value;
            if (!IsEnglish || table == null) return spanish;
            // Game state messages often contain counts. Match a translated format key before
            // falling back to individual words, so grammar and word order remain correct.
            var values = new List<object>();
            string formatKey = numberPattern.Replace(spanish, match =>
            {
                values.Add(match.Value);
                return "{" + (values.Count - 1) + "}";
            });
            if (values.Count > 0)
            {
                StringTableEntry formatEntry = table.GetEntry(formatKey);
                if (formatEntry != null && !string.IsNullOrEmpty(formatEntry.Value))
                    return string.Format(CultureInfo.InvariantCulture, formatEntry.Value, values.ToArray());
            }
            if (fragmentTable != table) RebuildFragments(table);
            string result = spanish;
            foreach (KeyValuePair<Regex, string> fragment in fragments)
                result = fragment.Key.Replace(result, fragment.Value);
            return result;
        }

        private static void RebuildFragments(StringTable table)
        {
            fragmentTable = table;
            fragments.Clear();
            foreach (string source in FragmentKeys)
            {
                StringTableEntry entry = table.GetEntry(source);
                if (entry == null || string.IsNullOrEmpty(entry.Value)) continue;
                fragments.Add(new KeyValuePair<Regex, string>(
                    new Regex(@"(?<!\p{L})" + Regex.Escape(source) + @"(?!\p{L})", RegexOptions.CultureInvariant),
                    entry.Value));
            }
        }

        public static string F(string spanishFormat, params object[] values) =>
            string.Format(CultureInfo.InvariantCulture, T(spanishFormat), values);

        public static bool HasKey(string spanish)
        {
            if (string.IsNullOrEmpty(spanish)) return false;
            StringTable table = ReadyTable();
            return table != null && table.GetEntry(spanish) != null;
        }

        public static AsyncOperationHandle<StringTable> LoadTableAsync() =>
            LocalizationSettings.StringDatabase.GetTableAsync(TableName);

        private static StringTable ReadyTable()
        {
            AsyncOperationHandle<StringTable> operation = LoadTableAsync();
            return operation.IsDone && operation.Status == AsyncOperationStatus.Succeeded
                ? operation.Result : null;
        }
    }

    /// <summary>Reapplies a static caption when the selected Unity locale changes.</summary>
    public sealed class SpaceFarmLocalizedLabel : MonoBehaviour
    {
        public string SpanishSource;
        private TMP_Text label;

        public void Configure(string source)
        {
            SpanishSource = source;
            Apply(null);
        }

        public void Refresh() => Apply(null);

        private void OnEnable()
        {
            label = GetComponent<TMP_Text>();
            LocalizationSettings.SelectedLocaleChanged += Apply;
        }

        private void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= Apply;

        private void Apply(Locale unused)
        {
            if (label != null) label.text = SpaceFarmLocalization.T(SpanishSource);
        }
    }
}
