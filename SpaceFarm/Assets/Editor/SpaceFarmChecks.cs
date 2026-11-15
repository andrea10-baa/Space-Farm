using System;
using UnityEditor;
using UnityEngine;

namespace SpaceFarm.Editor
{
    public static class SpaceFarmChecks
    {
        [MenuItem("Space Farm/Comprobar reglas básicas")]
        public static void Run()
        {
            CheckGeneratedZones();
            CheckProgression();
            CheckWorkersAndUpgrades();
            CheckMinesFollowDeposits();
            CheckSharedStorageAndDelivery();
            CheckTimedRepair();
            CheckEditingRules();
            CheckSaveSnapshotRoundTrip();
            CheckVersionedFileSave();
            CheckEconomyAndWalls();
            CheckShipments();
            Debug.Log("SPACE_FARM_CHECKS_OK: mapa, progresión, trabajadores, mejoras, economía, muros y entregas únicas.");
        }

        private static void CheckGeneratedZones()
        {
            for (int seed = -25; seed < 75; seed++)
            {
                SpaceFarmMap map = SpaceFarmMap.Generate(seed);
                Require(map.Validate(out string error), $"semilla {seed}: {error}");
                Require(map.Signature == SpaceFarmMap.Generate(seed).Signature, "la semilla debe ser reproducible");
                for (int zone = 1; zone <= 5; zone++)
                {
                    bool iron = false, ice = false;
                    foreach (MapDeposit deposit in map.Deposits)
                    {
                        if (deposit.Distance <= SpaceFarmMap.ZoneOuterDistance[zone - 1] ||
                            deposit.Distance > SpaceFarmMap.ZoneOuterDistance[zone]) continue;
                        iron |= deposit.Kind == DepositKind.Iron;
                        ice |= deposit.Kind == DepositKind.Ice;
                    }
                    Require(iron && ice, $"semilla {seed}: faltan recursos en nivel {zone}");
                }
                SpaceFarmState state = new SpaceFarmState(map);
                foreach (MapDeposit deposit in map.Deposits)
                    if (deposit.Distance > state.RoverRange)
                        Require(!state.CanRoverReach(deposit.Cell), "nivel 1 no alcanza zonas posteriores");
            }
        }

        private static void CheckProgression()
        {
            SpaceFarmMap map = SpaceFarmMap.Generate(42);
            SpaceFarmState state = new SpaceFarmState(map);
            Require(state.ColonyLevel == 1 && state.Population == 2 && state.RoverRange == 11 &&
                state.HabitatCapacity == 2, "estado inicial de progresión");
            int previousRange = state.RoverRange;
            int[] thresholds = { 25, 70, 140, 240 };
            foreach (int threshold in thresholds)
            {
                state.GainXp(threshold - state.Xp);
                Require(state.ColonyLevel == Array.IndexOf(thresholds, threshold) + 2, "umbral de nivel");
                Require(state.Population == state.ColonyLevel * 2 && state.Crew.Count == state.Population,
                    "dos astronautas y dos camas adicionales por nivel");
                Require(state.RoverRange > previousRange, "el alcance aumenta con cada nivel");
                previousRange = state.RoverRange;
            }
            foreach (MapDeposit deposit in map.Deposits)
                Require(state.CanRoverReach(deposit.Cell), "nivel 5 cubre todo el mapa");
            Require(state.XpToNext == 0 && state.ColonyLevel == 5, "nivel máximo");
            Require(!state.AddColonist(out _), "la llegada de astronautas depende del nivel");
        }

        private static void CheckWorkersAndUpgrades()
        {
            SpaceFarmState state = new SpaceFarmState();
            state.SurveyDeposit(0);
            Require(state.BuildMine(0, out _), "la primera mina se puede construir");
            Require(state.AssignWorker(CrewJob.Mine, 0, out _) && state.FreeWorkers == 1, "asignación a mina");
            state.AdvanceSol();
            Require(state.Deposit(0).MineStored == 8 && state.Deposit(0).LastResult.Nominal == 8,
                "un trabajador activo acelera la mina");
            state.Tick(95f);
            Require(state.Crew[0].Resting && state.ActiveWorkers(CrewJob.Mine, 0) == 0,
                "el astronauta agotado se retira a dormir");
            state.Tick(33f);
            Require(!state.Crew[0].Resting && state.Crew[0].Energy >= 95f,
                "el descanso recupera energía automáticamente");
            Require(state.AssignWorker(CrewJob.Mine, 0, out _) && state.FreeWorkers == 2,
                "la misma acción retira la asignación");
            Require(state.UpgradeMine(0, out _) && state.Deposit(0).MineLevel == 1,
                "mejora de mina comienza sin aplicarse instantáneamente");
            Require(!state.UpgradeMine(0, out _), "no se cobra dos veces una mejora en curso");
            state.Tick(29f);
            Require(state.Deposit(0).MineLevel == 1 && state.UpgradeRemaining("mine", 0) == 1,
                "faltando un segundo la mejora sigue pendiente");
            state.Tick(1f);
            Require(state.Deposit(0).MineLevel == 2 && state.UpgradeRemaining("mine", 0) == 0,
                "mejora terminada a los treinta segundos");
            Require(state.UpgradeRover(out _) && state.RoverCapacity == 12, "rover en mejora conserva su capacidad");
            state.Tick(30f);
            Require(state.RoverCapacity == 24, "rover mejorado carga más hierro");
            SpaceFarmState solar = new SpaceFarmState();
            Require(solar.UpgradeSolar(out _) && solar.SolarLevel == 1, "paneles en mejora");
            solar.Tick(30f);
            Require(solar.SolarLevel == 2 && solar.SolarPerSol == 26 && solar.EnergyCapacity == 40,
                "paneles mejorados aumentan generación y batería");
            Require(solar.AssignCrew(2, CrewJob.Solar, 0, out _) && solar.ActiveWorkers(CrewJob.Solar, 0) == 1,
                "se puede elegir a una persona concreta para los paneles");
            Require(!solar.AssignCrew(1, CrewJob.Solar, 0, out _), "una instalación no admite dos astronautas");
            solar.AdvanceSol();
            Require(solar.LastSol.Solar.Nominal == 31, "el astronauta aumenta la producción solar en 5 por sol");
            Require(solar.AssignCrew(2, CrewJob.None, -1, out _) && solar.FreeWorkers == 2,
                "la persona elegida puede dejar su tarea");

            SpaceFarmState instances = new SpaceFarmState();
            instances.AddMaterial(MaterialResource.Iron, 100);
            instances.SurveyDeposit(0);
            Require(instances.BuildMine(0, out _), "la mina individual empieza en nivel 1");
            Require(instances.PlaceStructure(StructureKind.Greenhouse, new Vector2Int(10, 10), true, out _) &&
                instances.PlaceStructure(StructureKind.Greenhouse, new Vector2Int(20, 20), true, out _),
                "se crean dos invernaderos independientes");
            instances.GainXp(70);
            Require(instances.UpgradeMine(0, out _) && instances.UpgradeGreenhouse(0, out _),
                "las mejoras individuales arrancan por instancia");
            instances.Tick(30f);
            Require(instances.Deposit(0).MineLevel == 2 && instances.GreenhouseLevel(0) == 2 && instances.GreenhouseLevel(1) == 1,
                "mejorar una mina y un invernadero no altera las otras instancias");
            Require(instances.PlaceStructure(StructureKind.Greenhouse, new Vector2Int(30, 30), true, out _) &&
                instances.GreenhouseLevel(2) == 1,
                "una instalación construida con la colonia avanzada sigue empezando en nivel 1");
        }

        private static void CheckEconomyAndWalls()
        {
            SpaceFarmState walls = new SpaceFarmState();
            int iron = walls.Iron;
            for (int i = 0; i < 13; i++)
                Require(walls.BuildBarrierSegment(new Vector2Int(100 + i, 100), false, true, out _),
                    "se pueden colocar más de doce muros");
            Require(walls.BarrierCount == 13 && walls.Iron == iron - 13, "cada muro cobra exactamente un hierro");
            Require(!walls.BuildBarrierSegment(new Vector2Int(100, 100), false, true, out _) &&
                walls.Iron == iron - 13, "un muro repetido no cobra");
            while (walls.Sol < 5) walls.AdvanceSol();
            Require(walls.LastSol.RaidFoodLoss == 1, "muros cercanos reducen incursión");

            SpaceFarmState economy = new SpaceFarmState();
            economy.GainXp(70);
            Require(economy.Population == 6, "nivel tres suma seis consumidores");
            economy.SurveyDeposit(0);
            economy.SurveyDeposit(1);
            Require(economy.BuildMine(0, out _) && economy.BuildWaterExtractor(1, out _),
                "el hierro inicial permite comenzar extracción de hierro y agua");
            Require(economy.PlaceStructure(StructureKind.Greenhouse, new Vector2Int(10, 10), true, out _),
                "primer invernadero");
            Require(economy.PlaceStructure(StructureKind.OxygenPlant, new Vector2Int(20, 20), true, out _),
                "segunda planta de oxígeno");
            Require(economy.AssignCrew(1, CrewJob.Oxygen, 0, out _) &&
                economy.AssignCrew(2, CrewJob.Extractor, 1, out _) &&
                economy.AssignCrew(3, CrewJob.Greenhouse, 0, out _) &&
                economy.AssignCrew(4, CrewJob.Mine, 0, out _), "cada astronauta se asigna a una estructura concreta");
            economy.AdvanceSol();
            Require(economy.LastSol.CrewConsumed == 6 && economy.Oxygen == 14 && economy.Food == 14 &&
                economy.Water == 15 && economy.OxygenLocal(0) > 0 && economy.GreenhouseLocal(0) > 0 &&
                economy.Deposit(1).WaterStored > 0 && economy.Deposit(0).MineStored == 8,
                "la producción se queda en depósitos locales hasta que el rover la entrega al almacén");
            Require(economy.LastSol.OxygenUnits.Count == 2 && economy.LastSol.GreenhouseUnits.Count == 1,
                "cada instalación presenta su propia producción");
            SpaceFarmState grace = new SpaceFarmState();
            for (int i = 0; i < 4; i++) grace.AdvanceSol();
            Require(!grace.GameOver && grace.Oxygen > 0 && grace.Water > 0 && grace.Food > 0,
                "las reservas iniciales dan varios soles para descubrir y construir");
        }

        private static void CheckMinesFollowDeposits()
        {
            MapDeposit[] layout = new MapDeposit[5];
            for (int i = 0; i < 4; i++)
                layout[i] = new MapDeposit { Id = i, Kind = DepositKind.Iron, Distance = i + 1, Name = "Fe-" + (i + 1) };
            layout[4] = new MapDeposit { Id = 4, Kind = DepositKind.Ice, Distance = 5, Name = "Hielo-1" };
            SpaceFarmState state = new SpaceFarmState(layout, Array.Empty<int>());
            state.AddMaterial(MaterialResource.Iron, 100);
            for (int i = 0; i < 4; i++)
            {
                state.SurveyDeposit(i);
                Require(state.MineBlocker(i).Length == 0, "con recursos, cualquier yacimiento estudiado admite una mina sin operador asignado");
                Require(state.BuildMine(i, out _), "se puede construir una mina en el yacimiento " + i);
                Require(!state.BuildMine(i, out _), "un yacimiento no puede recibir una segunda mina");
            }
            Require(state.MineCount == 4 && state.MaxMines == 4,
                "el total de minas depende solo de los cuatro depósitos de hierro generados");
        }

        private static void CheckSaveSnapshotRoundTrip()
        {
            SpaceFarmMap map = SpaceFarmMap.Generate(62417);
            SpaceFarmState original = new SpaceFarmState(map);
            MapDeposit iron = map.Nearest(DepositKind.Iron);
            original.SurveyDeposit(iron.Id);
            Require(original.BuildMine(iron.Id, out _), "preparar una colonia para guardar");
            Require(original.AssignCrew(1, CrewJob.Mine, iron.Id, out _), "asignación incluida en guardado");
            Require(original.UpgradeMine(iron.Id, out _), "mejora pendiente incluida en guardado");
            original.Tick(11f);
            original.GainXp(25);
            original.AdvanceSol();
            SpaceFarmState.SaveSnapshot snapshot = original.CaptureSave();

            SpaceFarmState restored = new SpaceFarmState(SpaceFarmMap.Generate(62417));
            Require(restored.RestoreSave(snapshot, out string error), "restauración compatible: " + error);
            Require(restored.Sol == original.Sol && restored.Iron == original.Iron && restored.Xp == original.Xp &&
                restored.ColonyLevel == original.ColonyLevel, "el guardado conserva recursos y progreso de colonia");
            Require(restored.Deposit(iron.Id).Surveyed && restored.Deposit(iron.Id).MineLevel == original.Deposit(iron.Id).MineLevel &&
                restored.Crew[0].Job == CrewJob.Mine && restored.UpgradeRemaining("mine", iron.Id) == original.UpgradeRemaining("mine", iron.Id),
                "el guardado conserva yacimiento, trabajador y mejora pendiente");
        }

        private static void CheckSharedStorageAndDelivery()
        {
            SpaceFarmState state = new SpaceFarmState();
            Require(state.WarehouseUsed == 96 && state.WarehouseCapacity >= state.WarehouseUsed,
                "las reservas iniciales caben dentro del almacén compartido");
            int accepted = state.AddMaterial(MaterialResource.Oxygen, 100);
            Require(accepted == 24 && state.WarehouseUsed == state.WarehouseCapacity && state.AddMaterial(MaterialResource.Water, 5) == 0,
                "los cuatro recursos comparten la capacidad y rechazan entradas cuando no hay espacio");

            SpaceFarmState delivery = new SpaceFarmState();
            delivery.AdvanceSol();
            int local = delivery.OxygenLocal(0);
            int reserve = delivery.Oxygen;
            Require(local > 0 && delivery.LoadResourceToRover(MaterialResource.Oxygen, 0, out _),
                "el rover puede cargar oxígeno desde el depósito local");
            int loaded = delivery.RoverOxygenCargo;
            Require(loaded > 0 && delivery.Oxygen == reserve && delivery.OxygenLocal(0) == local - loaded,
                "recoger no cambia las reservas utilizables ni duplica la carga");
            Require(delivery.UnloadRover(out _) && delivery.Oxygen == reserve + loaded && delivery.RoverCargoTotal == 0 &&
                delivery.WarehouseUsed <= delivery.WarehouseCapacity,
                "el oxígeno pasa a las reservas solo al descargarlo en el almacén");
        }

        private static void CheckTimedRepair()
        {
            SpaceFarmState state = new SpaceFarmState();
            SpaceFarmState.SaveSnapshot snapshot = state.CaptureSave();
            snapshot.DamagedKind = (int)DamagedStructureKind.Solar;
            snapshot.DamagedTarget = 0;
            Require(state.RestoreSave(snapshot, out _), "preparar una avería para la prueba de reparación");
            Require(state.RepairDamagedStructure(out _) && state.IsDamaged(DamagedStructureKind.Solar) && state.RepairSecondsRemaining == 30,
                "la reparación cobra e inicia un temporizador sin arreglar de inmediato");
            state.Tick(29f);
            Require(state.IsDamaged(DamagedStructureKind.Solar) && state.RepairSecondsRemaining == 1,
                "la estructura sigue averiada hasta terminar los treinta segundos");
            state.Tick(1f);
            Require(state.DamagedStructure == DamagedStructureKind.None,
                "la avería desaparece cuando termina la reparación");
        }

        private static void CheckEditingRules()
        {
            SpaceFarmMap map = SpaceFarmMap.Generate(4182);
            SpaceFarmState state = new SpaceFarmState(map);
            state.AddMaterial(MaterialResource.Iron, 24);
            Vector2Int first = FindStructureCell(state, map, StructureKind.Greenhouse, new Vector2Int(10, 10));
            Require(first.x >= 0 && state.PlaceStructure(StructureKind.Greenhouse, first, true, out _), "crear una instalación para editar");
            Require(state.AssignCrew(1, CrewJob.Greenhouse, 0, out _), "asignar una persona a la instalación movible");
            Vector2Int second = FindStructureCell(state, map, StructureKind.Greenhouse, new Vector2Int(30, 30));
            Require(second.x >= 0 && state.PlaceStructure(StructureKind.Greenhouse, second, true, out _), "crear segunda instalación movible");
            Vector2Int moved = FindEditCell(state, map, StructureKind.Greenhouse, 0, new Vector2Int(50, 50));
            Require(moved.x >= 0 && state.MoveStructure(StructureKind.Greenhouse, 0, moved, true, out _), "mover una instalación a una celda válida");
            Require(state.GreenhouseCells[0] == moved && state.GreenhouseLevel(0) == 1 &&
                state.WorkerOn(CrewJob.Greenhouse, 0) != null,
                "mover conserva nivel y trabajador asignado");
            Require(state.RemoveStructure(StructureKind.Greenhouse, 1, out _) && state.GreenhouseCells.Count == 1,
                "eliminar una instalación vacía conserva las demás");

            SpaceFarmState walls = new SpaceFarmState();
            Require(walls.BuildBarrierSegment(new Vector2Int(40, 40), false, true, out _) &&
                walls.MoveStructure(StructureKind.Barrier, 0, new Vector2Int(41, 40), true, out _) &&
                walls.Barriers[0].Cell == new Vector2Int(41, 40), "los muros se pueden mover y su celda cambia");
            Require(walls.RemoveStructure(StructureKind.Barrier, 0, out _) && walls.BarrierCount == 0,
                "los muros se pueden eliminar con reembolso calculado por el modelo");
        }

        private static Vector2Int FindStructureCell(SpaceFarmState state, SpaceFarmMap map, StructureKind kind, Vector2Int origin)
        {
            for (int radius = 0; radius < SpaceFarmMap.Width; radius++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int cell = origin + new Vector2Int(x, y);
                if (map.Inside(cell) && state.PlacementBlocker(kind, cell, true).Length == 0) return cell;
            }
            return new Vector2Int(-1, -1);
        }

        private static Vector2Int FindEditCell(SpaceFarmState state, SpaceFarmMap map, StructureKind kind, int index, Vector2Int origin)
        {
            for (int radius = 1; radius < SpaceFarmMap.Width; radius++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int cell = origin + new Vector2Int(x, y);
                if (map.Inside(cell) && state.EditMoveBlocker(kind, index, cell, true).Length == 0) return cell;
            }
            return new Vector2Int(-1, -1);
        }

        private static void CheckVersionedFileSave()
        {
            string id = "test-" + Guid.NewGuid().ToString("N");
            SpaceFarmState state = new SpaceFarmState();
            SpaceFarmSaveData data = new SpaceFarmSaveData
            {
                Id = id, Name = "Test slot", Seed = 17, RoverX = 3, RoverY = 4,
                Revealed = new bool[SpaceFarmMap.Width * SpaceFarmMap.Height], State = state.CaptureSave()
            };
            try
            {
                SpaceFarmSaveStore.Write(data);
                SpaceFarmSaveData read = SpaceFarmSaveStore.Read(id);
                Require(read != null && read.Seed == 17 && read.Name == data.Name && read.Version == SpaceFarmSaveData.CurrentVersion,
                    "un slot independiente se escribe y se lee con esquema versionado");
                data.Name = "Test slot actualizado";
                SpaceFarmSaveStore.Write(data);
                read = SpaceFarmSaveStore.Read(id);
                Require(read != null && read.Name == data.Name, "actualizar un slot reemplaza el archivo sin duplicarlo");
            }
            finally { SpaceFarmSaveStore.Delete(id); }
            Require(SpaceFarmSaveStore.Read(id) == null, "eliminar un slot lo retira de la lista de guardados");
        }

        private static void CheckShipments()
        {
            SpaceFarmState shipment = new SpaceFarmState();
            int ironBeforeOrder = shipment.Iron;
            Require(shipment.OrderFromEarth(out _) && shipment.ShipmentArrivalSol == 3 &&
                shipment.Iron == ironBeforeOrder - 4, "un pedido válido reserva su costo y fecha de llegada");
            int ironAfterOrder = shipment.Iron;
            Require(!shipment.OrderFromEarth(out _) && shipment.Iron == ironAfterOrder,
                "un segundo pedido en tránsito no cobra recursos ni duplica la entrega");

            shipment.AdvanceSol();
            Require(!shipment.LastSol.ShipmentDelivered && shipment.ShipmentArrivalSol == 3,
                "el pedido no se entrega antes del sol indicado");
            shipment.AdvanceSol();
            Require(shipment.LastSol.ShipmentDelivered && shipment.ShipmentArrivalSol == 0 &&
                shipment.Water == 21 && shipment.Food == 21,
                "el pedido entrega exactamente 5 agua y 5 comida al llegar");
            shipment.AdvanceSol();
            Require(!shipment.LastSol.ShipmentDelivered && shipment.Water == 19 && shipment.Food == 19,
                "los soles posteriores no vuelven a entregar los suministros");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("SPACE_FARM_CHECK_FAILED: " + message);
        }
    }
}
