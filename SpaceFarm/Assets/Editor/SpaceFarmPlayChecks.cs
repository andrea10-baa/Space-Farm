using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SpaceFarm.Editor
{
    /// <summary>
    /// Plays the real scene in Play Mode and checks the flows that rule tests cannot see: seeds, fog,
    /// survey, build, collect, pause in the middle of a trip, minimap and both restarts. It also saves
    /// the screenshots used in the docs. Run from the menu, or from the command line with
    /// -executeMethod SpaceFarm.Editor.SpaceFarmPlayChecks.Run (add -sfExit to close the Editor at the end).
    /// </summary>
    [InitializeOnLoad]
    public static class SpaceFarmPlayChecks
    {
        private const string ActiveKey = "SpaceFarm.PlayChecks.Active";
        private const string ResultKey = "SpaceFarm.PlayChecks.Result";
        private const string ScenePath = "Assets/Scenes/SpaceFarm.unity";
        private static readonly int[] Seeds = { 1, 42, 12345 };
        private static readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
        private static readonly List<string> Lines = new List<string>();
        private static bool started;

        static SpaceFarmPlayChecks()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                // Statics are reset when Play Mode starts: ask for the first seed again before the scene loads.
                SpaceFarmGame.RequestSeed(Seeds[0]);
                EditorApplication.update += Tick;
                return;
            }
            int result = SessionState.GetInt(ResultKey, -1);
            if (result < 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.EraseInt(ResultKey);
            if (Environment.GetCommandLineArgs().Contains("-sfExit"))
                EditorApplication.delayCall += () => EditorApplication.Exit(result);
        }

        [MenuItem("Space Farm/Probar partida en Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Salí de Play Mode antes de lanzar la prueba."); return; }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.delayCall += () =>
            {
                PlayModeWindow.SetCustomRenderingResolution(1280, 720, "Space Farm 1280x720");
                EditorApplication.EnterPlaymode();
            };
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (!started)
                {
                    started = true;
                    Stack.Push(Session());
                }
                while (Stack.Count > 0)
                {
                    IEnumerator top = Stack.Peek();
                    if (!top.MoveNext()) { Stack.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { Stack.Push(nested); continue; }
                    return;
                }
                Finish(0, "SPACE_FARM_PLAY_OK");
            }
            catch (Exception exception)
            {
                Finish(1, "SPACE_FARM_PLAY_FAILED: " + exception.Message + "\n" + exception.StackTrace);
            }
        }

        private static void Finish(int code, string message)
        {
            EditorApplication.update -= Tick;
            Lines.Add(message);
            string report = string.Join("\n", Lines);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/space-farm-play-checks.txt", report);
            if (code == 0) Debug.Log(report); else Debug.LogError(report);
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(ResultKey, code);
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }

        private static void Note(string line)
        {
            Lines.Add(line);
            Debug.Log("SPACE_FARM_PLAY: " + line);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Lines.Add("ok  " + message);
        }

        private static SpaceFarmGame Game()
        {
            SpaceFarmGame[] games = UnityEngine.Object.FindObjectsByType<SpaceFarmGame>();
            return games.Length == 1 && games[0].enabled && games[0].State != null ? games[0] : null;
        }

        private static IEnumerator Until(Func<bool> condition, double seconds, string what)
        {
            double limit = EditorApplication.timeSinceStartup + seconds;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > limit) throw new TimeoutException("Tiempo agotado: " + what);
                yield return null;
            }
        }

        private static IEnumerator Wait(double seconds)
        {
            double limit = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < limit) yield return null;
        }

        private static IEnumerator Shot(string name)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/screenshots"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            yield return Wait(.35);
            ScreenCapture.CaptureScreenshot(path);
            yield return Until(() => File.Exists(path), 10, "captura " + name);
            yield return Wait(.2);
            Note("captura " + name + ".png");
        }

        private static int SceneObjects() =>
            UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;

        private static void PressCrewButton(GameObject panel, string captionPrefix)
        {
            foreach (Button button in panel.GetComponentsInChildren<Button>())
            {
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label == null || !label.text.StartsWith(captionPrefix, StringComparison.Ordinal)) continue;
                Require(button.interactable, captionPrefix + " está disponible en el panel");
                button.onClick.Invoke();
                return;
            }
            throw new InvalidOperationException("No existe el botón de tripulación: " + captionPrefix);
        }

        private static int IronTotal(SpaceFarmGame game)
        {
            int total = game.State.Iron + game.State.RoverCargo;
            foreach (SpaceFarmState.DepositState deposit in game.State.Deposits) total += deposit.MineStored;
            return total;
        }

        /// <summary>Every deposit marker, label and minimap marker must be off while its cell is under fog.</summary>
        private static void RequireFogHidesDeposits(SpaceFarmGame game, string tag)
        {
            Transform root = game.transform.Find("Marte/Yacimientos");
            int hidden = 0;
            foreach (MapDeposit deposit in game.Map.Deposits)
            {
                if (game.IsRevealed(deposit.Cell)) continue;
                hidden++;
                string kind = deposit.Kind == DepositKind.Iron ? "Yacimiento " : "Depósito ";
                Require2(!root.Find(kind + deposit.Name).GetComponent<SpriteRenderer>().enabled, tag + deposit.Name + " visible bajo la niebla");
                Require2(!root.Find("Etiqueta " + deposit.Name).gameObject.activeSelf, tag + "etiqueta de " + deposit.Name + " visible bajo la niebla");
                Transform mini = game.transform.Find("Space Farm HUD/Minimapa/Vista/" + deposit.Name);
                Require2(mini != null && !mini.gameObject.activeSelf, tag + deposit.Name + " visible en el minimapa bajo la niebla");
            }
            Lines.Add($"ok  {tag}{hidden} yacimientos ocultos no se muestran en el mundo ni en el minimapa");
        }

        private static void Require2(bool condition, string failure)
        {
            if (!condition) throw new InvalidOperationException(failure);
        }

        private static IEnumerator Session()
        {
            yield return Until(() => Game() != null, 60, "inicio de la partida");
            yield return Wait(1);

            // --- Seeds: the scene must build exactly the map the generator gives for each seed.
            foreach (int seed in Seeds)
            {
                SpaceFarmGame current = Game();
                if (current.Seed != seed)
                {
                    current.RestartWithSeed(seed);
                    yield return Until(() => Game() != null && Game().Seed == seed, 30, "mapa de la semilla " + seed);
                    yield return Wait(.8);
                }
                SpaceFarmGame game = Game();
                SpaceFarmMap expected = SpaceFarmMap.Generate(seed);
                Require(game.Map.Signature == expected.Signature, $"semilla {seed}: la escena usa el mapa del generador ({expected.Signature:X16})");
                Require(game.State.Deposits.Count == expected.Deposits.Count, $"semilla {seed}: {expected.Deposits.Count} yacimientos funcionales, {expected.Points.Count} puntos de interés");
                RequireFogHidesDeposits(game, $"semilla {seed}: ");
                Note($"semilla {seed}: base {game.Map.BaseCell}, hierro cercano a {game.Map.Nearest(DepositKind.Iron).Distance}, hielo cercano a {game.Map.Nearest(DepositKind.Ice).Distance}, objetos en escena {SceneObjects()}");
                yield return Shot($"mapa-semilla-{seed}");
            }

            // --- Full flow on seed 1.
            Game().RestartWithSeed(1);
            yield return Until(() => Game() != null && Game().Seed == 1 && Game().State.Sol == 1, 30, "reinicio a la semilla 1");
            yield return Wait(.8);
            SpaceFarmGame colony = Game();
            colony.BeginFromTitleForTools();
            Vector3 arrivalCameraPosition = colony.CameraWorldPosition;
            colony.OrderRover(colony.Map.BaseCell + new Vector2Int(3, 0));
            colony.PressAdvanceSol();
            Require(colony.ArrivalSequenceActive && colony.State.Sol == 1 && !colony.RoverBusy &&
                colony.CameraWorldPosition == arrivalCameraPosition,
                "durante el aterrizaje inicial se bloquean órdenes y el sol, sin mover la cámara");
            yield return Until(() => !colony.ArrivalSequenceActive, 8, "aterrizaje inicial");
            Require(colony.transform.Find("Marte/Nave y hábitat").gameObject.activeSelf &&
                colony.transform.Find("Marte/Planta de oxígeno").gameObject.activeSelf,
                "la base y la planta aparecen al terminar el aterrizaje");
            Transform rockRoot = colony.transform.Find("Marte/Rocas y grava marciana");
            Require(rockRoot != null && rockRoot.GetComponent<MeshFilter>() != null &&
                rockRoot.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0 &&
                rockRoot.GetComponentsInChildren<MeshRenderer>().Length == 1,
                "las rocas del mapa se renderizan como una malla combinada");
            Vector2Int roverAtStart = colony.RoverCell;
            colony.OrderRover(new Vector2Int(1, 1));
            Require(colony.RoverCell == roverAtStart && !colony.RoverBusy,
                "nivel 1 impide viajar fuera del alcance de la base");
            int ironAtStart = colony.State.Iron;
            colony.OpenConstructionForTools();
            Require(colony.IsConstructionMenuOpen && !colony.RoverBusy,
                "abrir el catálogo no ordena mover al rover");
            colony.CloseConstructionForTools();
            yield return Wait(.1);
            Require(!colony.IsConstructionMenuOpen && colony.RoverCell == roverAtStart,
                "cerrar el catálogo deja al rover en su sector");
            colony.BeginPlacementForTools(StructureKind.Greenhouse);
            Require(colony.IsPlacingStructure && colony.State.Iron == ironAtStart,
                "elegir un plano no cobra recursos");
            colony.ConfirmPlacementForTools(colony.Map.BaseCell);
            Require(colony.IsPlacingStructure && colony.State.Iron == ironAtStart,
                "intentar construir sobre la base no cobra recursos");
            colony.SetPaused(true);
            Require(!colony.IsPlacingStructure && !colony.IsConstructionMenuOpen,
                "pausar cancela el plano y cierra el catálogo");
            colony.SetPaused(false);
            yield return Wait(.1);
            int initialObjects = SceneObjects();
            ulong initialSignature = colony.Map.Signature;
            MapDeposit iron = colony.Map.Nearest(DepositKind.Iron);
            MapDeposit ice = colony.Map.Nearest(DepositKind.Ice);

            colony.OrderRover(iron.Cell);
            yield return Until(() => !colony.RoverBusy, 30, "viaje al hierro");
            Require(colony.State.Deposit(iron.Id).Surveyed && colony.RoverCell == iron.Cell, "el rover descubre y estudia el yacimiento cercano");
            colony.PressDepositPrimary(iron.Id);
            Require(colony.IsPlacingStructure && colony.State.MineCount == 0, "elegir una mina solo activa el plano");
            colony.ConfirmPlacementForTools(iron.Cell);
            Require(colony.State.Deposit(iron.Id).MineLevel == 1 && colony.State.MineCount == 1, "se construye una mina en ese yacimiento desde su ficha");
            colony.OrderRover(ice.Cell);
            yield return Until(() => !colony.RoverBusy, 40, "viaje al hielo");
            Require(colony.State.Deposit(ice.Id).Surveyed, "el rover estudia el depósito de hielo cercano");
            colony.PressDepositPrimary(ice.Id);
            colony.ConfirmPlacementForTools(ice.Cell);
            Require(colony.State.Deposit(ice.Id).Extractor, "se construye un extractor en ese depósito");
            colony.PressAdvanceSol();
            Require(colony.State.Sol == 2 && colony.State.Deposit(iron.Id).MineStored == 5, "la mina produce 5 Fe en su almacén al avanzar el sol");
            RequireFogHidesDeposits(colony, "tras explorar: ");

            // --- Pause in the middle of a collection trip.
            int ironBefore = colony.State.Iron;
            int totalBefore = IronTotal(colony);
            colony.SelectDepositForTools(iron.Id);
            colony.OrderCollection(iron.Id);
            yield return Wait(.6);
            Require(colony.RoverBusy, "el rover sale a recoger de la mina elegida");
            colony.SetPaused(true);
            yield return null;
            Vector3 frozen = colony.RoverWorldPosition;
            int solBefore = colony.State.Sol;
            yield return Shot("menu-pausa-1280x720");
            yield return Wait(1.5);
            colony.OrderRover(colony.Map.BaseCell + new Vector2Int(6, 6));
            colony.PressAdvanceSol();
            colony.OrderCollection(iron.Id);
            colony.PressDepositPrimary(iron.Id);
            yield return Wait(.3);
            Require(colony.IsPaused && Time.timeScale == 0f, "la partida queda en pausa");
            Require(colony.RoverWorldPosition == frozen, "pausar durante el viaje detiene al rover");
            Require(colony.State.Sol == solBefore && IronTotal(colony) == totalBefore && colony.State.MineCount == 1,
                "en pausa no se puede avanzar el sol, construir ni recoger");
            colony.SetPaused(false);
            yield return Until(() => !colony.RoverBusy, 60, "fin de la recogida tras reanudar");
            Require(colony.State.Iron == ironBefore + 5 && colony.State.RoverCargo == 0 &&
                colony.State.Deposit(iron.Id).MineStored == 0 && IronTotal(colony) == totalBefore,
                "al reanudar la recogida termina y el hierro no se pierde ni se duplica");

            // --- Minimap: fog, navigation and corner preference.
            Vector3 cameraBefore = colony.CameraWorldPosition;
            Vector2Int unexplored = new Vector2Int(1, 1);
            Require(!colony.IsRevealed(unexplored), "la esquina del mapa sigue oculta");
            colony.PressMinimapCell(unexplored);
            Require(colony.CameraWorldPosition == cameraBefore, "un clic en zona oculta del minimapa no mueve la cámara");
            Vector2Int roverBefore = colony.RoverCell;
            colony.PressMinimapCell(ice.Cell);
            yield return Wait(.2);
            Require(colony.CameraWorldPosition != cameraBefore && colony.RoverCell == roverBefore && !colony.RoverBusy,
                "un clic en zona explorada centra la cámara y no mueve al rover");
            colony.FocusCell(colony.Map.BaseCell);
            colony.SetMinimapExpanded(false);
            colony.SetMinimapSide(false);
            colony.SelectDepositForTools(iron.Id);
            yield return Shot("minimapa-izquierda-1280x720");
            colony.SetMinimapSide(true);
            colony.SetMinimapExpanded(true);
            Require(colony.MinimapOnRight && colony.MinimapExpanded, "el minimapa cambia de esquina y de tamaño");
            yield return Shot("minimapa-derecha-ampliado-1280x720");
            PlayModeWindow.SetCustomRenderingResolution(1060, 480, "Space Farm 1060x480");
            yield return Wait(.6);
            yield return Shot("minimapa-derecha-ampliado-1060x480");
            colony.SetMinimapSide(false);
            colony.SetMinimapExpanded(false);
            yield return Shot("minimapa-izquierda-1060x480");
            colony.SetPaused(true);
            yield return Shot("menu-pausa-1060x480");
            colony.SetPaused(false);
            PlayModeWindow.SetCustomRenderingResolution(1280, 720, "Space Farm 1280x720");
            yield return Wait(.6);

            // --- Existing mechanics still run through the scene: barriers, colonist, storm, raid.
            Vector2Int wallBase = colony.Map.BaseCell + new Vector2Int(4, -2);
            for (int i = 0; i < 4 && colony.State.Iron > 0; i++)
                colony.State.BuildBarrierSegment(wallBase + new Vector2Int(0, i), false,
                    colony.IsRevealed(wallBase + new Vector2Int(0, i)), out _);
            Require(colony.State.Population >= 4 && colony.State.ColonyLevel >= 2,
                "la exploración y las construcciones otorgan XP y dos astronautas");
            colony.RevealForTools(colony.Map.BaseCell, 0);
            while (colony.State.Sol < 5 && !colony.State.GameOver) colony.PressAdvanceSol();
            yield return Wait(.5);
            Require(colony.State.Sol == 5 && colony.State.LastSol.RaidFoodLoss > 0, "la tormenta y la incursión del sol 5 siguen ocurriendo");
            Require(colony.transform.Find("Marte").GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Astronauta")) == colony.State.Population,
                "los astronautas visibles coinciden con la población");

            // --- A wide look at the terrain for the docs.
            colony.State.GainXp(240 - colony.State.Xp);
            for (int y = 2; y < SpaceFarmMap.Height; y += 4)
            for (int x = 2; x < SpaceFarmMap.Width; x += 4)
                colony.RevealForTools(new Vector2Int(x, y), 4);
            MapCrater big = colony.Map.Craters[0];
            colony.FocusCell(new Vector2Int(Mathf.RoundToInt(big.X), Mathf.RoundToInt(big.Y)));
            yield return Wait(1.2);
            yield return Shot("crater-grande-semilla-1-1280x720");
            colony.SetMinimapExpanded(true);
            yield return Shot("minimapa-mapa-revelado-1280x720");
            colony.SetMinimapExpanded(false);

            // --- Restart with the same seed, then a new map.
            colony.RestartSameMap();
            yield return Until(() => Game() != null && Game() != colony, 30, "reinicio con la misma semilla");
            yield return Wait(.8);
            SpaceFarmGame again = Game();
            again.BeginFromTitleForTools();
            Require(again.ArrivalSequenceActive, "reiniciar vuelve a preparar la secuencia de aterrizaje");
            yield return Until(() => !again.ArrivalSequenceActive, 8, "aterrizaje después de reiniciar");
            Require(again.Seed == 1 && again.Map.Signature == initialSignature, "reiniciar este mapa conserva la semilla y el mapa");
            Require(again.State.Sol == 1 && again.State.Iron == 36 && again.State.MineCount == 0 && again.State.BarrierCount == 0 &&
                again.State.Population == 2 && !again.IsRevealed(iron.Cell + (iron.Cell - again.Map.BaseCell)),
                "reiniciar restaura recursos, edificios, barreras, colonos y niebla");
            Require(SceneObjects() == initialObjects, $"reiniciar no deja objetos duplicados ({initialObjects} objetos antes y después)");
            Require(UnityEngine.Object.FindObjectsByType<Canvas>().Length == 1 &&
                UnityEngine.Object.FindObjectsByType<SpaceFarmGame>(FindObjectsInactive.Include).Length == 1,
                "queda un solo HUD y una sola partida");
            Require(Time.timeScale == 1f && !again.IsPaused, "la partida reiniciada arranca sin pausa");

            again.RestartNewMap();
            yield return Until(() => Game() != null && Game() != again, 30, "mapa nuevo");
            yield return Wait(.8);
            SpaceFarmGame fresh = Game();
            fresh.BeginFromTitleForTools();
            yield return Until(() => !fresh.ArrivalSequenceActive, 8, "aterrizaje del mapa nuevo");
            Require(fresh.Seed != 1 && fresh.Map.Signature != initialSignature, $"empezar un mapa nuevo usa otra semilla ({fresh.Seed})");
            Require(fresh.State.Sol == 1 && fresh.State.MineCount == 0 &&
                UnityEngine.Object.FindObjectsByType<Canvas>().Length == 1 &&
                fresh.transform.Find("Marte/Yacimientos").childCount == fresh.Map.Deposits.Count * 3,
                "el mapa nuevo empieza limpio, sin objetos del anterior");
            Button crewToggle = GameObject.Find("ASTRONAUTAS")?.GetComponent<Button>();
            Require(crewToggle != null, "el HUD ofrece un botón propio para gestionar astronautas");
            crewToggle.onClick.Invoke();
            GameObject crewPanel = GameObject.Find("Administrar astronautas");
            Require(crewPanel != null && crewPanel.activeInHierarchy, "el botón abre el panel de tripulación");
            PressCrewButton(crewPanel, "A-02");
            PressCrewButton(crewPanel, "Paneles solares");
            Require(fresh.State.WorkerOn(CrewJob.Solar, 0)?.Id == 2, "el astronauta elegido queda asignado a los paneles");
            PressCrewButton(crewPanel, "DEJAR TAREA");
            PressCrewButton(crewPanel, "Planta de O2 1");
            Require(fresh.State.WorkerOn(CrewJob.Oxygen, 0)?.Id == 2 && fresh.State.WorkerOn(CrewJob.Solar, 0) == null,
                "el mismo astronauta puede cambiar de instalación sin duplicarse");
            yield return Shot("astronautas-1280x720");
            PressCrewButton(crewPanel, "DEJAR TAREA");
            PressCrewButton(crewPanel, "CERRAR");
            Require(!crewPanel.activeInHierarchy && fresh.State.FreeWorkers == 2,
                "cerrar el panel vuelve al mapa y libera a la persona elegida");

            // --- Earth shipment: state grants once; visuals pause and are destroyed on restart.
            Require(fresh.State.OrderFromEarth(out _) && fresh.State.ShipmentArrivalSol == fresh.State.Sol + 2,
                "el pedido conserva el plazo de dos soles");
            fresh.PressAdvanceSol();
            Require(!fresh.State.LastSol.ShipmentDelivered, "el primer sol no entrega antes de tiempo");
            fresh.PressAdvanceSol();
            Require(fresh.State.LastSol.ShipmentDelivered && fresh.State.ShipmentArrivalSol == 0 &&
                fresh.ShipmentSequenceActive,
                "al llegar, el estado entrega una vez y encola la animación de suministros");
            fresh.SetPaused(true);
            yield return Wait(.35);
            Require(fresh.IsPaused && fresh.ShipmentSequenceActive,
                "pausar conserva la animación de entrega sin bloquearla permanentemente");
            fresh.SetPaused(false);
            yield return Until(() => !fresh.ShipmentSequenceActive, 8, "despegue de la nave de suministros");
            int deliveredWater = fresh.State.Water;
            int deliveredFood = fresh.State.Food;
            fresh.PressAdvanceSol();
            Require(!fresh.State.LastSol.ShipmentDelivered && fresh.State.Water == deliveredWater - fresh.State.Population &&
                fresh.State.Food == deliveredFood - fresh.State.Population,
                "la entrega no vuelve a agregar recursos en el sol siguiente");

            Require(fresh.State.OrderFromEarth(out _), "se puede solicitar otro envío tras completar el anterior");
            fresh.PressAdvanceSol();
            fresh.PressAdvanceSol();
            Require(fresh.State.LastSol.ShipmentDelivered && fresh.ShipmentSequenceActive,
                "un pedido posterior inicia una segunda animación sin superponer la primera");
            fresh.RestartSameMap();
            yield return Until(() => Game() != null && Game() != fresh, 30, "reinicio durante una entrega");
            yield return Wait(.8);
            SpaceFarmGame restartedDuringShipment = Game();
            Require(!restartedDuringShipment.ShipmentSequenceActive && restartedDuringShipment.State.ShipmentArrivalSol == 0 &&
                restartedDuringShipment.transform.Find("Marte/Nave de suministros") == null,
                "reiniciar durante la animación elimina la nave y el pedido de la partida anterior");
            restartedDuringShipment.BeginFromTitleForTools();
            yield return Until(() => !restartedDuringShipment.ArrivalSequenceActive, 8, "aterrizaje posterior al reinicio de envío");
        }
    }
}
