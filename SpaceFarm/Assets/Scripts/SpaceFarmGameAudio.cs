using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>Procedural ambient music, alert tones and audio controls for Space Farm.</summary>
    public sealed partial class SpaceFarmGame
    {
        private const string MusicVolumeKey = "SpaceFarm.Audio.Music";
        private const string EffectsVolumeKey = "SpaceFarm.Audio.Effects";
        private const int AudioSampleRate = 22050;

        private AudioSource musicSource;
        private AudioSource effectsSource;
        private AudioClip damageAlarm;
        private GameObject damageAlertRoot;
        private TextMeshProUGUI damageAlertText;
        private Coroutine damageAlertRoutine;
        private TextMeshProUGUI musicVolumeValue;
        private TextMeshProUGUI effectsVolumeValue;
        private bool settingsFromTitle;
        private Button spanishButton;
        private Button englishButton;

        private void CreateAudioSystem()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = CreateAmbientMusic();
            runtimeAssets.Add(musicSource.clip);
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = PlayerPrefs.GetFloat(MusicVolumeKey, .28f);

            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.spatialBlend = 0f;
            effectsSource.volume = PlayerPrefs.GetFloat(EffectsVolumeKey, .72f);
            damageAlarm = CreateDamageAlarm();
            runtimeAssets.Add(damageAlarm);
            musicSource.Play();
        }

        private static AudioClip CreateAmbientMusic()
        {
            const int seconds = 32;
            int sampleCount = AudioSampleRate * seconds;
            float[] samples = new float[sampleCount];
            // A slow D / Bm / G / A progression, kept soft so the HUD and alarm remain clear.
            float[][] chords =
            {
                new[] { 146.83f, 220f, 293.66f, 369.99f },
                new[] { 123.47f, 185f, 246.94f, 293.66f },
                new[] { 98f, 146.83f, 196f, 246.94f },
                new[] { 110f, 164.81f, 220f, 277.18f }
            };
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)AudioSampleRate;
                float local = t % 8f;
                int chordIndex = Mathf.Min(3, (int)(t / 8f));
                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, local / 1.8f));
                float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (8f - local) / 1.8f));
                float envelope = Mathf.Min(fadeIn, fadeOut);
                float pad = 0f;
                for (int note = 0; note < chords[chordIndex].Length; note++)
                {
                    float frequency = chords[chordIndex][note];
                    float phase = t * frequency * Mathf.PI * 2f;
                    float shimmer = 1f + .0025f * Mathf.Sin(t * .22f + note * 1.7f);
                    pad += (Mathf.Sin(phase * shimmer) * .72f + Mathf.Sin(phase * 2f) * .12f) * .25f;
                }

                // Sparse, rounded notes add a gentle musical pulse without a beat-heavy loop.
                float beat = local % 2f;
                float pluckEnvelope = Mathf.Exp(-beat * 2.7f) * (beat < .035f ? beat / .035f : 1f);
                int pluck = ((int)(local / 2f) + chordIndex) % 4;
                float pluckHz = chords[chordIndex][pluck];
                float pluckTone = Mathf.Sin(t * pluckHz * Mathf.PI * 2f) * pluckEnvelope * .08f;
                samples[i] = Mathf.Clamp((pad * envelope * .075f + pluckTone), -.22f, .22f);
            }

            AudioClip clip = AudioClip.Create("Space Farm - Ambient Mars", sampleCount, 1, AudioSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateDamageAlarm()
        {
            const float duration = 2.25f;
            int count = Mathf.CeilToInt(AudioSampleRate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)AudioSampleRate;
                float pulse = t % .75f;
                float withinPulse = pulse / .75f;
                float frequency = Mathf.Lerp(620f, 1080f, withinPulse);
                float envelope = Mathf.Clamp01(Mathf.Min(pulse / .08f, (.67f - pulse) / .12f));
                float tone = Mathf.Sin(t * frequency * Mathf.PI * 2f) + .22f * Mathf.Sin(t * frequency * 2f * Mathf.PI * 2f);
                samples[i] = tone * envelope * .34f;
            }

            AudioClip clip = AudioClip.Create("Space Farm - Damage Alarm", count, 1, AudioSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void CreateDamageAlert(Transform canvas)
        {
            damageAlertRoot = Panel("Alerta de avería", canvas, new Color(.25f, .07f, .055f, .97f),
                new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0, -HeaderHeight - 14f), new Vector2(450, 62));
            damageAlertRoot.GetComponent<Image>().raycastTarget = false;
            GameObject border = Panel("Borde de alerta", damageAlertRoot.transform, SpaceFarmPalette.Danger,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), Vector2.zero, new Vector2(5, 0));
            border.GetComponent<Image>().raycastTarget = false;
            GameObject icon = Panel("Icono de alerta", damageAlertRoot.transform, SpaceFarmPalette.Danger,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(19, 0), new Vector2(30, 30));
            icon.GetComponent<Image>().raycastTarget = false;
            TextMeshProUGUI exclamation = Label("!", icon.transform, 22, FontStyles.Bold, SpaceFarmPalette.Text,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            exclamation.alignment = TextAlignmentOptions.Center;
            exclamation.raycastTarget = false;
            TextMeshProUGUI heading = Label("ALERTA · INSTALACIÓN AVERIADA", damageAlertRoot.transform,
                13, FontStyles.Bold, SpaceFarmPalette.Iron,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(62, -7), new Vector2(-76, 22));
            heading.raycastTarget = false;
            damageAlertText = Label("", damageAlertRoot.transform, 14, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, .5f), new Vector2(62, -9), new Vector2(-76, -22));
            damageAlertText.enableAutoSizing = true;
            damageAlertText.fontSizeMin = 10;
            damageAlertText.fontSizeMax = 14;
            damageAlertText.textWrappingMode = TextWrappingModes.NoWrap;
            damageAlertText.raycastTarget = false;
            damageAlertRoot.SetActive(false);
        }

        private void ShowDamageAlert(string structureName)
        {
            if (damageAlertRoot == null) return;
            damageAlertText.text = SpaceFarmLocalization.F("Se averió {0}. Enviá el rover para repararla.", SpaceFarmLocalization.T(structureName));
            damageAlertRoot.SetActive(true);
            damageAlertRoot.transform.SetAsLastSibling();
            if (effectsSource != null && damageAlarm != null)
            {
                effectsSource.Stop();
                effectsSource.PlayOneShot(damageAlarm);
            }
            if (damageAlertRoutine != null) StopCoroutine(damageAlertRoutine);
            damageAlertRoutine = StartCoroutine(HideDamageAlert());
        }

        private IEnumerator HideDamageAlert()
        {
            float elapsed = 0f;
            while (elapsed < 7f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (damageAlertRoot != null) damageAlertRoot.SetActive(false);
            damageAlertRoutine = null;
        }

        private void CreateAudioSettingsPage()
        {
            pauseAudioPage = Panel("Configuración", hudCanvas.transform,
                new Color(.04f, .07f, .1f, .88f), Vector2.zero, Vector2.one,
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            pauseAudioPage.GetComponent<Image>().raycastTarget = true;
            GameObject settingsCard = Panel("Panel de configuración", pauseAudioPage.transform,
                SpaceFarmPalette.Panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(400, 445));
            settingsCard.GetComponent<Image>().raycastTarget = true;
            Transform page = settingsCard.transform;
            PauseTitle(page, "CONFIGURACIÓN");
            AddAudioSlider(page, "Música de fondo", -68, true, out musicVolumeValue);
            AddAudioSlider(page, "Efectos y alarmas", -162, false, out effectsVolumeValue);
            TextMeshProUGUI languageTitle = Label("IDIOMA", page, 15, FontStyles.Bold,
                SpaceFarmPalette.Text, new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -257), new Vector2(250, 24));
            languageTitle.alignment = TextAlignmentOptions.Center;
            spanishButton = AddButton("ESPAÑOL", page, () => ChangeLanguage("es"), 14,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(-80, -307), new Vector2(146, 42));
            englishButton = AddButton("ENGLISH", page, () => ChangeLanguage("en"), 14,
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(80, -307), new Vector2(146, 42));
            RefreshLanguageButtons();
            float y = -379;
            PauseButton(page, "VOLVER", CloseSettings, SpaceFarmPalette.Iron,
                SpaceFarmPalette.Background, ref y);
            pauseAudioPage.SetActive(false);
        }

        private void ShowTitleSettings()
        {
            settingsFromTitle = true;
            RefreshLanguageButtons();
            pauseAudioPage.SetActive(true);
            pauseAudioPage.transform.SetAsLastSibling();
        }

        private void CloseSettings()
        {
            pauseAudioPage.SetActive(false);
            if (!settingsFromTitle) ShowPausePage(false);
        }

        private void ChangeLanguage(string code)
        {
            SpaceFarmLocalization.SetLanguage(code);
            StartCoroutine(FinishLanguageChange());
        }

        private IEnumerator FinishLanguageChange()
        {
            yield return SpaceFarmLocalization.LoadTableAsync();
            foreach (SpaceFarmLocalizedLabel label in hudCanvas.GetComponentsInChildren<SpaceFarmLocalizedLabel>(true))
                label.Refresh();
            RefreshLanguageButtons();
            upgradeCatalogSignature = string.Empty;
            if (state != null) RefreshHud();
            if (constructionMenuOpen) SelectCategory(selectedCategory);
            RefreshReportLanguage();
            if (constructionMenuToggle != null)
                constructionMenuToggle.GetComponentInChildren<TextMeshProUGUI>().text =
                    SpaceFarmLocalization.T(constructionMenuOpen ? "CERRAR MENÚ" : "CONSTRUIR");
            if (crewMenuToggle != null)
                crewMenuToggle.GetComponentInChildren<TextMeshProUGUI>().text =
                    SpaceFarmLocalization.T(crewMenuOpen ? "CERRAR EQUIPO" : "ASTRONAUTAS");
            if (actionMenuTitle != null)
                actionMenuTitle.text = SpaceFarmLocalization.T(new[] { "CONSTRUCCIÓN", "MEJORAS", "COLONIA" }[Mathf.Clamp(selectedCategory, 0, 2)]);
            if (pauseMinimapLabel != null)
                pauseMinimapLabel.text = SpaceFarmLocalization.T(minimapRight ? "MINIMAPA: DERECHA" : "MINIMAPA: IZQUIERDA");
            if (minimapTitleLabel != null)
                minimapTitleLabel.text = SpaceFarmLocalization.F("MAPA · semilla {0}", seed);
            if (infoPanel != null && infoPanel.activeSelf) RefreshEducationalCard();
        }

        private void RefreshLanguageButtons()
        {
            if (spanishButton == null || englishButton == null) return;
            spanishButton.GetComponent<Image>().color = SpaceFarmLocalization.IsEnglish
                ? SpaceFarmPalette.PanelRaised : SpaceFarmPalette.Oxygen;
            englishButton.GetComponent<Image>().color = SpaceFarmLocalization.IsEnglish
                ? SpaceFarmPalette.Oxygen : SpaceFarmPalette.PanelRaised;
        }

        private void AddAudioSlider(Transform page, string caption, float y, bool music, out TextMeshProUGUI valueLabel)
        {
            TextMeshProUGUI label = Label(caption, page, 15, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y), new Vector2(250, 24));
            label.alignment = TextAlignmentOptions.Center;
            valueLabel = Label("", page, 12, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y - 27), new Vector2(250, 18));
            valueLabel.alignment = TextAlignmentOptions.Center;
            TextMeshProUGUI sliderValueLabel = valueLabel;

            GameObject root = new GameObject(music ? "Volumen de música" : "Volumen de efectos",
                typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(page, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, y - 63);
            rect.sizeDelta = new Vector2(260, 24);

            GameObject track = Panel("Fondo del control", root.transform, SpaceFarmPalette.Background,
                new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 8));
            GameObject fillArea = new GameObject("Área de nivel", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform fillRect = fillArea.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, .5f); fillRect.anchorMax = new Vector2(1, .5f);
            fillRect.pivot = new Vector2(0, .5f); fillRect.sizeDelta = new Vector2(-8, 8);
            fillRect.anchoredPosition = Vector2.zero;
            GameObject fill = new GameObject("Nivel de volumen", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillImage = fill.GetComponent<RectTransform>();
            fillImage.anchorMin = Vector2.zero; fillImage.anchorMax = Vector2.one; fillImage.offsetMin = Vector2.zero; fillImage.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = SpaceFarmPalette.Oxygen;

            GameObject handleArea = new GameObject("Área del control", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero; handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(7, 0); handleAreaRect.offsetMax = new Vector2(-7, 0);
            GameObject handle = new GameObject("Control deslizante", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(18, 18);
            handle.GetComponent<Image>().color = SpaceFarmPalette.Ice;

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillImage;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = PlayerPrefs.GetFloat(music ? MusicVolumeKey : EffectsVolumeKey, music ? .28f : .72f);
            slider.onValueChanged.AddListener(value =>
            {
                if (music) SetMusicVolume(value); else SetEffectsVolume(value);
                sliderValueLabel.text = $"{Mathf.RoundToInt(value * 100f)} %";
            });
            valueLabel.text = $"{Mathf.RoundToInt(slider.value * 100f)} %";
        }

        private void SetMusicVolume(float value)
        {
            value = Mathf.Clamp01(value);
            if (musicSource != null) musicSource.volume = value;
            PlayerPrefs.SetFloat(MusicVolumeKey, value);
            PlayerPrefs.Save();
        }

        private void SetEffectsVolume(float value)
        {
            value = Mathf.Clamp01(value);
            if (effectsSource != null) effectsSource.volume = value;
            PlayerPrefs.SetFloat(EffectsVolumeKey, value);
            PlayerPrefs.Save();
        }
    }
}
