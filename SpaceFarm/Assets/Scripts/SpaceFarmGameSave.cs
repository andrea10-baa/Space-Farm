using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFarm
{
    /// <summary>Named local save slots for Space Farm sessions.</summary>
    public sealed partial class SpaceFarmGame
    {
        private static SpaceFarmSaveData pendingSessionLoad;
        private static string pendingSessionName;
        private string activeSessionId;
        private string activeSessionName;
        private bool loadedSavedSession;
        private GameObject saveDialog;
        private TMP_InputField saveNameInput;
        private Transform saveSlotsContent;

        public string SaveSlotForTools(string name)
        {
            activeSessionId = Guid.NewGuid().ToString("N");
            activeSessionName = name;
            SaveCurrentSession();
            return activeSessionId;
        }
        public void LoadSlotForTools(string id) => LoadSave(id);
        public void DeleteSlotForTools(string id) => SpaceFarmSaveStore.Delete(id);

        private static SpaceFarmSaveData TakePendingSessionLoad()
        {
            SpaceFarmSaveData data = pendingSessionLoad;
            pendingSessionLoad = null;
            return data;
        }

        private void RestoreRevealMask(bool[] mask)
        {
            if (mask == null || mask.Length != Width * Height) return;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) revealed[x, y] = mask[y * Width + x];
        }

        private bool[] CaptureRevealMask()
        {
            bool[] mask = new bool[Width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) mask[y * Width + x] = revealed[x, y];
            return mask;
        }

        private void SaveCurrentSession()
        {
            if (state == null || string.IsNullOrWhiteSpace(activeSessionName))
            {
                SetReport("Elegí un nombre para esta partida antes de guardarla.");
                return;
            }
            try
            {
                SpaceFarmSaveStore.Write(new SpaceFarmSaveData
                {
                    Version = SpaceFarmSaveData.CurrentVersion,
                    Id = activeSessionId,
                    Name = activeSessionName,
                    Seed = seed,
                    RoverX = roverCell.x,
                    RoverY = roverCell.y,
                    TargetX = roverDestination.x,
                    TargetY = roverDestination.y,
                    Revealed = CaptureRevealMask(),
                    State = state.CaptureSave()
                });
                SetReport($"Partida «{activeSessionName}» guardada en este dispositivo.");
                if (paused) ShowPausePage(false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetReport("No se pudo guardar la partida. El archivo anterior se conservó.");
            }
        }

        private void ShowNewGameNamePrompt()
        {
            DestroySaveDialog();
            saveDialog = Panel("Nombre de nueva partida", titleScreenRoot.transform,
                new Color(.025f, .045f, .065f, .94f), Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            saveDialog.GetComponent<Image>().raycastTarget = true;
            GameObject box = Panel("Formulario de partida", saveDialog.transform, SpaceFarmPalette.Panel,
                new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(520, 270));
            Label("NUEVA COLONIA", box.transform, 24, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),new Vector2(0,-24),new Vector2(-40,42)).alignment = TextAlignmentOptions.Center;
            TextMeshProUGUI help = Label("Dale un nombre para identificarla en la lista de partidas.", box.transform,
                13, FontStyles.Normal, SpaceFarmPalette.MutedText, new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),
                new Vector2(0,-75),new Vector2(-50,28));
            help.alignment = TextAlignmentOptions.Center;
            saveNameInput = CreateSaveNameInput(box.transform, "Nombre de la colonia", new Vector2(0,-126));
            TextMeshProUGUI error = Label("", box.transform, 12, FontStyles.Bold, SpaceFarmPalette.Danger,
                new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),new Vector2(0,-160),new Vector2(-40,22));
            error.alignment = TextAlignmentOptions.Center;
            AddButton("COMENZAR", box.transform, () =>
            {
                string name = saveNameInput.text.Trim();
                if (name.Length == 0) { error.text = "Escribí un nombre para comenzar."; return; }
                if (SpaceFarmSaveStore.NameExists(name)) { error.text = "Ya existe una partida con ese nombre."; return; }
                activeSessionName = name;
                DestroySaveDialog();
                BeginFromTitle();
            }, 14, SpaceFarmPalette.Iron, SpaceFarmPalette.Background,
                new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-92,15),new Vector2(180,40));
            AddButton("VOLVER", box.transform, DestroySaveDialog, 13, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(104,15),new Vector2(150,40));
            saveNameInput.ActivateInputField();
        }

        private TMP_InputField CreateSaveNameInput(Transform parent, string placeholder, Vector2 position)
        {
            GameObject field = Panel("Campo de nombre", parent, SpaceFarmPalette.Background,
                new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(.5f,1),position,new Vector2(390,40));
            // Panel() disables raycasts for decorative backgrounds. An input field needs its
            // background to receive pointer events so clicking it can give it keyboard focus.
            field.GetComponent<Image>().raycastTarget = true;
            TMP_InputField input = field.AddComponent<TMP_InputField>();
            TextMeshProUGUI text = Label("", field.transform, 16, FontStyles.Normal, SpaceFarmPalette.Text,
                Vector2.zero,Vector2.one,new Vector2(.5f,.5f),new Vector2(0,0),new Vector2(-22,-10));
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            TextMeshProUGUI hint = Label(placeholder, field.transform, 16, FontStyles.Italic, SpaceFarmPalette.MutedText,
                Vector2.zero,Vector2.one,new Vector2(.5f,.5f),new Vector2(0,0),new Vector2(-22,-10));
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            hint.raycastTarget = false;
            input.textComponent = text;
            input.textViewport = field.GetComponent<RectTransform>();
            input.placeholder = hint;
            input.targetGraphic = field.GetComponent<Image>();
            input.characterLimit = 28;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private void ShowSaveSlots()
        {
            DestroySaveDialog();
            saveDialog = Panel("Partidas guardadas", titleScreenRoot.transform,
                new Color(.025f,.045f,.065f,.96f), Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
            saveDialog.GetComponent<Image>().raycastTarget = true;
            GameObject box = Panel("Lista de partidas", saveDialog.transform, SpaceFarmPalette.Panel,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(700,500));
            Label("CONTINUAR PARTIDA", box.transform, 23, FontStyles.Bold, SpaceFarmPalette.Text,
                new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),new Vector2(0,-18),new Vector2(-48,38)).alignment = TextAlignmentOptions.Center;
            GameObject scroll = new GameObject("Desplazamiento partidas", typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(box.transform,false);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0,0); scrollRect.anchorMax = new Vector2(1,1);
            scrollRect.offsetMin = new Vector2(22,64); scrollRect.offsetMax = new Vector2(-22,-68);
            Image viewportImage = scroll.AddComponent<Image>(); viewportImage.color = SpaceFarmPalette.Background;
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scroll.transform,false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero; viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero; viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            GameObject content = new GameObject("Contenido partidas", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform,false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0,1); contentRect.anchorMax = new Vector2(1,1);
            contentRect.pivot = new Vector2(.5f,1); contentRect.offsetMin = Vector2.zero; contentRect.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 7; layout.padding = new RectOffset(8,8,8,8); layout.childControlWidth = true;
            layout.childControlHeight = false; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            saveSlotsContent = content.transform;
            ScrollRect scroller = scroll.GetComponent<ScrollRect>(); scroller.viewport = viewportRect; scroller.content = contentRect;
            scroller.horizontal = false; scroller.vertical = true; scroller.movementType = ScrollRect.MovementType.Clamped;
            List<SpaceFarmSaveData> saves = SpaceFarmSaveStore.List();
            if (saves.Count == 0)
            {
                TextMeshProUGUI empty = Label("Todavía no hay partidas guardadas en este dispositivo.", content.transform,
                    14,FontStyles.Normal,SpaceFarmPalette.MutedText,new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),
                    new Vector2(0,-20),new Vector2(-20,42));
                empty.alignment = TextAlignmentOptions.Center;
            }
            foreach (SpaceFarmSaveData data in saves) AddSaveSlot(data);
            AddButton("CERRAR", box.transform, DestroySaveDialog, 13, SpaceFarmPalette.PanelRaised, SpaceFarmPalette.Text,
                new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,15),new Vector2(190,38));
        }

        private void AddSaveSlot(SpaceFarmSaveData data)
        {
            GameObject row = Panel("Partida " + data.Name, saveSlotsContent, SpaceFarmPalette.PanelRaised,
                new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),Vector2.zero,new Vector2(-8,75));
            LayoutElement element = row.AddComponent<LayoutElement>(); element.minHeight = 75; element.preferredHeight = 75;
            string date = DateTime.TryParse(data.SavedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
                ? parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "fecha desconocida";
            int sol = data.State != null ? data.State.Sol : 1;
            int level = data.State != null ? data.State.ColonyLevel : 1;
            TextMeshProUGUI details = Label($"{data.Name}\nSol {sol} · nivel {level} · semilla {data.Seed} · {date}", row.transform,
                13,FontStyles.Normal,SpaceFarmPalette.Text,new Vector2(0,0),new Vector2(1,1),new Vector2(0,0),
                new Vector2(12,5),new Vector2(-265,-10));
            details.alignment = TextAlignmentOptions.MidlineLeft; details.enableAutoSizing = true; details.fontSizeMin = 10; details.fontSizeMax = 13;
            AddButton("CARGAR", row.transform, () => LoadSave(data.Id), 12, SpaceFarmPalette.Iron, SpaceFarmPalette.Background,
                new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-144,0),new Vector2(102,42));
            AddButton("BORRAR", row.transform, () => ConfirmDeleteSave(data), 12, SpaceFarmPalette.Danger, SpaceFarmPalette.Text,
                new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-42,0),new Vector2(82,42));
        }

        private void ConfirmDeleteSave(SpaceFarmSaveData data)
        {
            DestroySaveDialog();
            saveDialog = Panel("Confirmar eliminación", titleScreenRoot.transform,
                new Color(.025f,.045f,.065f,.96f),Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
            saveDialog.GetComponent<Image>().raycastTarget = true;
            GameObject box = Panel("Confirmación",saveDialog.transform,SpaceFarmPalette.Panel,
                new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(500,220));
            TextMeshProUGUI message = Label($"¿Eliminar la partida «{data.Name}»? Esta acción no se puede deshacer.",box.transform,
                16,FontStyles.Bold,SpaceFarmPalette.Text,new Vector2(0,1),new Vector2(1,1),new Vector2(.5f,1),
                new Vector2(0,-35),new Vector2(-50,75));
            message.alignment = TextAlignmentOptions.Center;
            AddButton("ELIMINAR",box.transform,() => { SpaceFarmSaveStore.Delete(data.Id); ShowSaveSlots(); },13,
                SpaceFarmPalette.Danger,SpaceFarmPalette.Text,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-92,25),new Vector2(170,42));
            AddButton("CANCELAR",box.transform,ShowSaveSlots,13,SpaceFarmPalette.PanelRaised,SpaceFarmPalette.Text,
                new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(96,25),new Vector2(170,42));
        }

        private void LoadSave(string id)
        {
            SpaceFarmSaveData save = SpaceFarmSaveStore.Read(id);
            if (save == null) { SetReport("El archivo de esa partida no se puede cargar."); ShowSaveSlots(); return; }
            pendingSessionLoad = save;
            Restart(true);
        }

        private void DestroySaveDialog()
        {
            if (saveDialog != null) Destroy(saveDialog);
            saveDialog = null;
            saveNameInput = null;
            saveSlotsContent = null;
        }
    }
}
