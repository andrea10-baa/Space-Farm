using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>
    /// Pause menu. Pausing sets Time.timeScale to zero, which freezes the rover, astronauts, particles and
    /// fog fade; Update stops reading world input, and a full-screen panel swallows every click.
    /// </summary>
    public sealed partial class SpaceFarmGame
    {
        private bool paused;
        private GameObject pauseOverlay;
        private GameObject pauseMainPage;
        private GameObject pauseControlsPage;
        private TextMeshProUGUI pauseMinimapLabel;

        public void SetPaused(bool value)
        {
            if (pauseOverlay == null || paused == value || arrivalSequenceActive) return;
            if (value)
            {
                HideWorldSelection();
                CancelPlacement(false);
                if (barrierMode) EndBarrierMode();
                CloseConstructionMenu();
                CloseCrewMenu();
            }
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            pauseOverlay.SetActive(value);
            pauseOverlay.transform.SetAsLastSibling();
            // Any press in progress is dropped so it cannot become an order when the game resumes.
            isPanning = false;
            leftPressValid = false;
            leftDragging = false;
            if (value) ShowPausePage(false);
        }

        private void ShowPausePage(bool controls)
        {
            pauseMainPage.SetActive(!controls);
            pauseControlsPage.SetActive(controls);
        }

        private void CreatePauseMenu()
        {
            pauseOverlay = Panel("Pausa", hudCanvas.transform, new Color(.04f, .07f, .1f, .82f),
                Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            // The dim layer is a raycast target: nothing behind it can be clicked while paused.
            pauseOverlay.GetComponent<Image>().raycastTarget = true;

            pauseMainPage = PausePanel("Menú", 400);
            Transform main = pauseMainPage.transform;
            PauseTitle(main, "PAUSA");
            TextMeshProUGUI seedLabel = Label($"Semilla de este mapa: <b>{seed}</b>", main, 13, FontStyles.Normal,
                SpaceFarmPalette.MutedText, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1),
                new Vector2(0, -58), new Vector2(-40, 22));
            seedLabel.alignment = TextAlignmentOptions.Center;
            float y = -92;
            PauseButton(main, "REANUDAR", () => SetPaused(false), SpaceFarmPalette.Iron, SpaceFarmPalette.Background, ref y);
            PauseButton(main, "REINICIAR ESTE MAPA", () => Restart(true), SpaceFarmPalette.Button, SpaceFarmPalette.Text, ref y);
            PauseButton(main, "EMPEZAR UN MAPA NUEVO", () => Restart(false), SpaceFarmPalette.Button, SpaceFarmPalette.Text, ref y);
            PauseButton(main, "CONTROLES", () => ShowPausePage(true), SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text, ref y);
            Button side = PauseButton(main, "MINIMAPA", () => SetMinimapSide(!minimapRight),
                SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text, ref y);
            pauseMinimapLabel = side.GetComponentInChildren<TextMeshProUGUI>();
            pauseMinimapLabel.text = minimapRight ? "MINIMAPA: DERECHA" : "MINIMAPA: IZQUIERDA";
#if !UNITY_WEBGL || UNITY_EDITOR
            // A browser tab cannot be closed by Application.Quit, so the web build simply has no exit button.
            PauseButton(main, "SALIR DEL JUEGO", QuitGame, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.MutedText, ref y);
#endif
            RectTransform mainRect = pauseMainPage.GetComponent<RectTransform>();
            mainRect.sizeDelta = new Vector2(mainRect.sizeDelta.x, -y + 14);

            pauseControlsPage = PausePanel("Controles", 392);
            Transform controls = pauseControlsPage.transform;
            PauseTitle(controls, "CONTROLES");
            TextMeshProUGUI help = Label(
                "<b>Cámara</b>  Arrastrá el mapa o usá WASD / flechas.\n" +
                "<b>Zoom</b>  Rueda del mouse. VOLVER A LA BASE e IR AL ROVER reubican la vista; SEGUIR acompaña al rover.\n" +
                "<b>Explorar</b>  Clic en el terreno: el rover viaja ahí y revela sectores. Clic en una señal: va a estudiarla.\n" +
                "<b>Construir</b>  Abrí CONSTRUIR. Elegí un plano y hacé clic en un sector válido, o arrastrá una tarjeta al mapa. Esc o clic derecho cancelan.\n" +
                "<b>Astronautas</b>  Abrí ASTRONAUTAS, elegí una persona y luego una instalación. Trabajar aumenta su producción; descansan solos al cansarse.\n" +
                "<b>Muros</b>  Elegí muros en el menú y colocá cada uno en un sector libre (1 Fe). Los vecinos se unen solos; Esc cancela.\n" +
                "<b>Minimapa</b>  Clic en zona explorada: centra la cámara sin mover al rover. + / - cambia el tamaño; < > lo pasa de lado.\n" +
                "<b>Pausa</b>  Esc o el botón PAUSA.",
                controls, 13, FontStyles.Normal, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -56), new Vector2(-44, 270));
            help.alignment = TextAlignmentOptions.TopLeft;
            help.enableAutoSizing = true;
            help.fontSizeMin = 10;
            help.fontSizeMax = 13;
            help.lineSpacing = 12;
            float back = -338;
            PauseButton(controls, "VOLVER", () => ShowPausePage(false), SpaceFarmPalette.Iron, SpaceFarmPalette.Background, ref back);
            pauseControlsPage.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 392);

            pauseControlsPage.SetActive(false);
            pauseOverlay.SetActive(false);
        }

        private GameObject PausePanel(string name, float height)
        {
            GameObject panel = Panel(name, pauseOverlay.transform, SpaceFarmPalette.Panel,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(380, height));
            panel.GetComponent<Image>().raycastTarget = true;
            Panel("Acento", panel.transform, SpaceFarmPalette.Iron, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, 1), Vector2.zero, new Vector2(0, 3));
            return panel;
        }

        private void PauseTitle(Transform parent, string caption)
        {
            TextMeshProUGUI title = Label(caption, parent, 26, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(-40, 36));
            title.alignment = TextAlignmentOptions.Center;
        }

        private Button PauseButton(Transform parent, string caption, System.Action callback, Color color, Color textColor, ref float y)
        {
            Button button = AddButton(caption, parent, callback, 14, color, textColor,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y), new Vector2(300, 40));
            y -= 47;
            return button;
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
