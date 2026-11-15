using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>Visual title screen. The live Martian map remains behind the menu as its backdrop.</summary>
    public sealed partial class SpaceFarmGame
    {
        private GameObject titleScreenRoot;
        private bool titleScreenOpen;
        public bool TitleScreenOpen => titleScreenOpen;
        public void BeginFromTitleForTools() => BeginFromTitle();
        private readonly Dictionary<GameObject, bool> titleVisibilityBeforeMenu = new Dictionary<GameObject, bool>();

        private void CreateTitleScreen()
        {
            titleScreenOpen = true;
            for (int i = 0; i < hudCanvas.transform.childCount; i++)
            {
                GameObject child = hudCanvas.transform.GetChild(i).gameObject;
                if (child.name == "EventSystem") continue;
                titleVisibilityBeforeMenu[child] = child.activeSelf;
                child.SetActive(false);
            }
            titleScreenRoot = Panel("Pantalla inicial", hudCanvas.transform,
                new Color(.035f, .065f, .085f, .82f), Vector2.zero, Vector2.one,
                new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            titleScreenRoot.GetComponent<Image>().raycastTarget = true;
            CanvasGroup inputGate = titleScreenRoot.AddComponent<CanvasGroup>();
            inputGate.interactable = false;
            inputGate.blocksRaycasts = true;

            Panel("Placa de portada", titleScreenRoot.transform, new Color(.07f, .11f, .14f, 1f),
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(620, 430));

            // Fine framing lines and warm/cool marks keep the screen in the game's Martian palette.
            Panel("Línea superior", titleScreenRoot.transform, SpaceFarmPalette.Iron,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 196), new Vector2(430, 2));
            Panel("Línea inferior", titleScreenRoot.transform, SpaceFarmPalette.Ice,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -196), new Vector2(430, 2));

            TextMeshProUGUI missionMark = Label("EXPEDICIÓN 01     /     MARTE",
                titleScreenRoot.transform, 12, FontStyles.Bold, SpaceFarmPalette.Ice,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 151), new Vector2(400, 24));
            missionMark.alignment = TextAlignmentOptions.Center;
            missionMark.characterSpacing = 3;

            TextMeshProUGUI title = Label("SPACE FARM", titleScreenRoot.transform,
                48, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 93), new Vector2(540, 66));
            title.alignment = TextAlignmentOptions.Center;
            title.enableAutoSizing = true;
            title.fontSizeMin = 36;
            title.fontSizeMax = 48;
            title.characterSpacing = 2;

            TextMeshProUGUI subtitle = Label("CULTIVÁ · EXPLORÁ · SOBREVIVÍ",
                titleScreenRoot.transform, 14, FontStyles.Bold, SpaceFarmPalette.Iron,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 52), new Vector2(420, 26));
            subtitle.alignment = TextAlignmentOptions.Center;
            subtitle.characterSpacing = 1.5f;

            TextMeshProUGUI intro = Label("Una nueva colonia comienza en el polvo rojo.",
                titleScreenRoot.transform, 15, FontStyles.Normal, SpaceFarmPalette.MutedText,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 15), new Vector2(470, 30));
            intro.alignment = TextAlignmentOptions.Center;

            Button play = AddButton("INICIAR EXPEDICIÓN", titleScreenRoot.transform,
                ShowNewGameNamePrompt, 17, SpaceFarmPalette.Button, SpaceFarmPalette.Text,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -58), new Vector2(310, 48));
            play.transition = Selectable.Transition.ColorTint;
            ColorBlock playColors = play.colors;
            playColors.normalColor = SpaceFarmPalette.Button;
            playColors.highlightedColor = SpaceFarmPalette.ButtonHover;
            playColors.pressedColor = SpaceFarmPalette.Iron;
            playColors.selectedColor = SpaceFarmPalette.ButtonHover;
            play.colors = playColors;

            GameObject continuePanel = Panel("Continuar partida", titleScreenRoot.transform,
                SpaceFarmPalette.PanelRaised,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -124), new Vector2(310, 42));
            TextMeshProUGUI continueLabel = Label("CONTINUAR", continuePanel.transform,
                14, FontStyles.Bold, SpaceFarmPalette.MutedText,
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, new Vector2(-12, -6));
            continueLabel.alignment = TextAlignmentOptions.Center;
            UnityEngine.UI.Button continueButton = continuePanel.AddComponent<UnityEngine.UI.Button>();
            continueButton.targetGraphic = continuePanel.GetComponent<Image>();
            continuePanel.GetComponent<Image>().raycastTarget = true;
            continueButton.onClick.AddListener(ShowSaveSlots);
            continueButton.interactable = SpaceFarmSaveStore.List().Count > 0;
            continueLabel.color = continueButton.interactable ? SpaceFarmPalette.Ice : SpaceFarmPalette.MutedText;
            ColorBlock continueColors = continueButton.colors;
            continueColors.normalColor = SpaceFarmPalette.PanelRaised;
            continueColors.highlightedColor = SpaceFarmPalette.ButtonHover;
            continueColors.pressedColor = SpaceFarmPalette.Iron;
            continueButton.colors = continueColors;

            TextMeshProUGUI futureLabel = Label("PARTIDAS LOCALES", titleScreenRoot.transform,
                9, FontStyles.Bold, SpaceFarmPalette.MutedText,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -156), new Vector2(250, 16));
            futureLabel.alignment = TextAlignmentOptions.Center;
            futureLabel.characterSpacing = 2;

            TextMeshProUGUI seedLabel = Label("NUEVA REGIÓN  ·  ASENTAMIENTO PENDIENTE",
                titleScreenRoot.transform, 11, FontStyles.Bold, SpaceFarmPalette.MutedText,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -178), new Vector2(420, 24));
            seedLabel.alignment = TextAlignmentOptions.Center;
            seedLabel.characterSpacing = 1.2f;

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            StartCoroutine(EnableTitleInput(inputGate));
        }

        private static IEnumerator EnableTitleInput(CanvasGroup inputGate)
        {
            yield return new WaitForSecondsRealtime(.25f);
            if (inputGate != null) inputGate.interactable = true;
        }

        private void BeginFromTitle()
        {
            if (!titleScreenOpen) return;
            DestroySaveDialog();
            titleScreenOpen = false;
            titleScreenRoot.SetActive(false);
            foreach (KeyValuePair<GameObject, bool> visibility in titleVisibilityBeforeMenu)
            {
                if (visibility.Key != null) visibility.Key.SetActive(visibility.Value);
            }
            titleVisibilityBeforeMenu.Clear();
            if (loadedSavedSession)
            {
                startingBaseLanded = true;
                SetStartingBaseVisible(true);
                RefreshWorld();
                RefreshHud();
                SetReport($"Partida «{activeSessionName}» cargada desde el sol {state.Sol}. El rover quedó en su última casilla confirmada.");
            }
            else BeginInitialLanding();
        }
    }
}
