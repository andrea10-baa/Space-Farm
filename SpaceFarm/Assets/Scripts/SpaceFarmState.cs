using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFarm
{
    public enum StructureKind { None, Mine, Extractor, Greenhouse, OxygenPlant, Barrier, Warehouse }
    public enum CrewJob { None, Mine, Extractor, Greenhouse, Oxygen, Solar }
    public enum DamagedStructureKind { None, Solar, Dormitory, Greenhouse, Rover, Mine }
    public enum MaterialResource { Iron, Oxygen, Water, Food }
    public enum GreenhouseCrop { Wheat, Lettuce, Corn }

    /// <summary>Small deterministic simulation for the colony loop. Knows deposits by id, not by map position.</summary>
    [Serializable]
    public sealed class SpaceFarmState
    {
        public const int MaxLevel = 5;
        public const float UpgradeSeconds = 30f;
        private static readonly int[] LevelXp = { 0, 0, 25, 70, 140, 240 };
        private static readonly int[] RoverRanges = { 0, 11, 24, 39, 56, 999 };
        public const int MaxExtractors = 5;
        public const int MaxWarehouses = 5;
        // Initial reserves occupy 96 units (36 Fe + 20 each of O2, water and food).
        // The first warehouse therefore starts with headroom for production and shipments.
        public const int WarehouseStorage = 120;
        public const int LocalStorageCapacity = 24;
        private readonly List<BarrierState> barriers = new List<BarrierState>();
        private readonly List<Vector2Int> greenhouseCells = new List<Vector2Int>();
        private readonly List<Vector2Int> oxygenCells = new List<Vector2Int>();
        private readonly List<Vector2Int> warehouseCells = new List<Vector2Int>();
        private readonly List<int> warehouseLevels = new List<int>();
        private readonly List<int> oxygenLocal = new List<int>();
        private readonly List<int> greenhouseLocal = new List<int>();
        private readonly List<int> oxygenLevels = new List<int>();
        private readonly List<int> greenhouseLevels = new List<int>();
        private readonly List<GreenhouseCrop> greenhouseCrops = new List<GreenhouseCrop>();
        private readonly List<int> greenhouseCropProgress = new List<int>();
        private readonly List<CrewMember> crew = new List<CrewMember>();
        private readonly List<UpgradeTask> upgrades = new List<UpgradeTask>();
        private readonly bool[] discoveries;
        private readonly int[] discoveryKinds;
        private readonly List<DepositState> deposits = new List<DepositState>();
        private readonly SpaceFarmMap map;
        private SpaceFarmRandom eventRandom;
        private int lastStormSol = -100;

        public sealed class SystemResult
        {
            public int Nominal;
            public int Actual;
            public int EnergyUsed;
            public int OtherUsed;
            public string Reason = "Aún no se avanzó un sol.";
        }

        /// <summary>One iron or ice deposit with the installation built on it. Each keeps its own level and storage.</summary>
        public sealed class DepositState
        {
            public int Id { get; internal set; }
            public DepositKind Kind { get; internal set; }
            public int Distance { get; internal set; }
            public string Name { get; internal set; }
            public bool Surveyed { get; internal set; }
            public int MineLevel { get; internal set; }
            public int MineStored { get; internal set; }
            public int WaterStored { get; internal set; }
            public bool Extractor { get; internal set; }
            public int ExtractorLevel { get; internal set; }
            public SystemResult LastResult { get; internal set; } = new SystemResult();
            public int MineCapacity => MineLevel > 0 ? 18 + 18 * (MineLevel - 1) : 0;
            public int MineNominal => MineLevel > 0 ? 5 + 3 * (MineLevel - 1) : 0;
            public int MineEnergy => MineLevel > 0 ? 1 + MineLevel : 0;
            public bool HasBuilding => MineLevel > 0 || Extractor;
        }

        public sealed class BarrierState
        {
            public Vector2Int Cell { get; internal set; }
            public bool Vertical { get; internal set; }
        }

        public sealed class CrewMember
        {
            public int Id { get; internal set; }
            public CrewJob Job { get; internal set; }
            public int Target { get; internal set; } = -1;
            public float Energy { get; internal set; } = 100f;
            public bool Resting { get; internal set; }
        }

        public sealed class UpgradeTask
        {
            public string Kind { get; internal set; }
            public int Target { get; internal set; }
            public float Remaining { get; internal set; }
            public float Duration { get; internal set; }
        }

        public sealed class SolResults
        {
            public readonly SystemResult Solar = new SystemResult();
            public readonly SystemResult OxygenOne = new SystemResult();
            public readonly SystemResult OxygenTwo = new SystemResult();
            /// <summary>Total of every extractor this sol.</summary>
            public readonly SystemResult Water = new SystemResult();
            public readonly SystemResult Greenhouse = new SystemResult();
            /// <summary>Total of every mine this sol.</summary>
            public readonly SystemResult Mine = new SystemResult();
            public readonly List<SystemResult> OxygenUnits = new List<SystemResult>();
            public readonly List<SystemResult> GreenhouseUnits = new List<SystemResult>();
            public bool Storm;
            public bool ShipmentDelivered;
            public int CrewConsumed;
            public int RaidFoodLoss;
            public DamagedStructureKind DamagedStructure;
            public int DamagedTarget = -1;
        }

        [Serializable]
        public sealed class SaveSnapshot
        {
            public int Sol, Iron, Energy, Oxygen, Water, Food, Population, ColonyLevel, Xp, Health;
            public int SolarLevel, BaseOxygenLevel, OxygenPlants, BaseOxygenStored, RoverLevel;
            public int RoverCargo, RoverOxygenCargo, RoverWaterCargo, RoverFoodCargo, ShipmentArrivalSol;
            public int ShipmentFoodRemaining, ShipmentWaterRemaining;
            public int DamagedKind, DamagedTarget, LastStormSol;
            public bool HasStartingOxygenCell;
            public CellSnapshot StartingOxygenCell;
            public bool AutomaticCollection;
            public int[] OxygenLevels, GreenhouseLevels, WarehouseLevels, OxygenLocal, GreenhouseLocal;
            public int[] GreenhouseCrops, GreenhouseCropProgress;
            public int[] Discoveries;
            public DepositSnapshot[] Deposits;
            public CrewSnapshot[] Crew;
            public UpgradeSnapshot[] Upgrades;
            public CellSnapshot[] Barriers, Greenhouses, OxygenCells, WarehouseCells;
            public string LastReport;
            public ulong EventRandomState;
        }

        [Serializable] public sealed class DepositSnapshot
        {
            public int Surveyed, MineLevel, MineStored, Extractor, ExtractorLevel, WaterStored;
        }
        [Serializable] public sealed class CrewSnapshot
        {
            public int Id, Job, Target;
            public float Energy;
            public bool Resting;
        }
        [Serializable] public sealed class UpgradeSnapshot
        {
            public string Kind;
            public int Target;
            public float Remaining, Duration;
        }
        [Serializable] public struct CellSnapshot { public int X, Y; public bool Vertical; }

        public SolResults LastSol { get; private set; } = new SolResults();
        public int Sol { get; private set; } = 1;
        public int Iron { get; private set; } = 36;
        public int Energy { get; private set; } = 20;
        public int Oxygen { get; private set; } = 20;
        public int Water { get; private set; } = 20;
        public int Food { get; private set; } = 20;
        public int Population { get; private set; } = 2;
        public int ColonyLevel { get; private set; } = 1;
        public int Xp { get; private set; }
        public int XpToNext => ColonyLevel >= MaxLevel ? 0 : LevelXp[ColonyLevel + 1];
        public int XpAtLevelStart => LevelXp[ColonyLevel];
        public float ColonyLevelProgress => ColonyLevel >= MaxLevel ? 1f : Mathf.Clamp01((float)(Xp - XpAtLevelStart) / Mathf.Max(1, XpToNext - XpAtLevelStart));
        public int HabitatCapacity => ColonyLevel * 2;
        public int RoverRange => RoverRanges[ColonyLevel];
        public int SolarLevel { get; private set; } = 1;
        public int BaseOxygenLevel { get; private set; } = 1;
        public int EnergyCapacity => 30 + (SolarLevel - 1) * 10;
        public int SolarPerSol => 14 + (SolarLevel - 1) * 12;
        public int Health { get; private set; } = 3;
        public int OxygenPlants { get; private set; } = 1;
        public int RoverLevel { get; private set; } = 1;
        public bool Greenhouse { get; private set; }
        public Vector2Int? GreenhouseCell { get; private set; }
        public Vector2Int? OxygenPlantTwoCell { get; private set; }
        public Vector2Int StartingOxygenCell { get; private set; }
        public int BarrierCount => barriers.Count;
        public int RoverCargo { get; private set; }
        public int RoverOxygenCargo { get; private set; }
        public int RoverWaterCargo { get; private set; }
        public int RoverFoodCargo { get; private set; }
        public int RoverCargoTotal => RoverCargo + RoverOxygenCargo + RoverWaterCargo + RoverFoodCargo;
        public int RoverCapacity => RoverLevel == 2 ? 24 : 12;
        public int ShipmentArrivalSol { get; private set; }
        public int ShipmentFoodRemaining { get; private set; }
        public int ShipmentWaterRemaining { get; private set; }
        public DamagedStructureKind DamagedStructure { get; private set; }
        public int DamagedTarget { get; private set; } = -1;
        public int RepairIronCost => RepairCost(DamagedStructure);
        public int RepairEnergyCost => DamagedStructure == DamagedStructureKind.None ? 0 : 3;
        public bool IsRepairing => HasUpgrade("repair", RepairTaskTarget);
        public int RepairTaskTarget => DamagedStructure == DamagedStructureKind.None ? 0 : Mathf.Max(0, DamagedTarget);
        public int RepairSecondsRemaining => IsRepairing ? UpgradeRemaining("repair", RepairTaskTarget) : 0;
        public int WarehouseCapacity
        {
            get { int total = 0; for (int i = 0; i < warehouseLevels.Count; i++) total += WarehouseStorage + (warehouseLevels[i] - 1) * 40; return total; }
        }
        public int WarehouseUsed => Iron + Oxygen + Water + Food;
        public int WarehouseFree => Mathf.Max(0, WarehouseCapacity - WarehouseUsed);
        public bool AutomaticCollection { get; set; } = true;
        public int WarehouseCount => warehouseCells.Count;
        public bool GameOver => Health <= 0;
        public bool MissionComplete => Sol >= 7 && MineCount > 0 && Greenhouse && ColonyLevel >= 3 && !GameOver;
        public int ScanRadius => 1 + ColonyLevel + (RoverLevel > 1 ? 1 : 0);
        public int OxygenPerSol => OxygenPlants * 7;
        public static int WorkerBonus(CrewJob job) => job == CrewJob.Solar ? 5 : job == CrewJob.None ? 0 : 3;
        public string LastReport { get; private set; } = "La nave aterrizó. Enviá el rover a explorar los sectores ocultos.";

        public IReadOnlyList<DepositState> Deposits => deposits;
        public int DiscoveryCount => discoveries.Length;
        public IReadOnlyList<BarrierState> Barriers => barriers;
        public IReadOnlyList<Vector2Int> GreenhouseCells => greenhouseCells;
        public IReadOnlyList<Vector2Int> OxygenCells => oxygenCells;
        public IReadOnlyList<Vector2Int> WarehouseCells => warehouseCells;
        public int OxygenLevel(int index) => index >= 0 && index < OxygenPlants ? (index == 0 ? BaseOxygenLevel : oxygenLevels[index - 1]) : 0;
        public int GreenhouseLevel(int index) => index >= 0 && index < greenhouseLevels.Count ? greenhouseLevels[index] : 0;
        public GreenhouseCrop GreenhouseCropAt(int index) => index >= 0 && index < greenhouseCrops.Count ? greenhouseCrops[index] : GreenhouseCrop.Wheat;
        public int GreenhouseCropProgress(int index) => index >= 0 && index < greenhouseCropProgress.Count ? greenhouseCropProgress[index] : 0;
        public static string GreenhouseCropName(GreenhouseCrop crop) => crop == GreenhouseCrop.Lettuce ? "Lechuga" : crop == GreenhouseCrop.Corn ? "Maíz" : "Trigo";
        public static int GreenhouseCropUnlockLevel(GreenhouseCrop crop) => crop == GreenhouseCrop.Corn ? 3 : crop == GreenhouseCrop.Lettuce ? 2 : 1;
        public static int GreenhouseCropDuration(GreenhouseCrop crop) => crop == GreenhouseCrop.Corn ? 3 : crop == GreenhouseCrop.Lettuce ? 2 : 1;
        public int GreenhouseCropYield(int index)
        {
            return GreenhouseCropYield(GreenhouseCropAt(index), GreenhouseLevel(index));
        }
        public static int GreenhouseCropYield(GreenhouseCrop crop, int level)
        {
            int baseYield = crop == GreenhouseCrop.Corn ? 20 : crop == GreenhouseCrop.Lettuce ? 16 : 7;
            return Mathf.Min(LocalStorageCapacity, baseYield + Mathf.Max(0, level - GreenhouseCropUnlockLevel(crop)) * 2);
        }
        public bool SetGreenhouseCrop(int index, GreenhouseCrop crop, out string message)
        {
            if (index < 0 || index >= greenhouseCells.Count) return Fail("Seleccioná un invernadero válido.", out message);
            if (GreenhouseCropUnlockLevel(crop) > GreenhouseLevel(index))
                return Fail($"{GreenhouseCropName(crop)} se desbloquea en el nivel {GreenhouseCropUnlockLevel(crop)}.", out message);
            if (greenhouseCrops[index] == crop) return Success($"El invernadero {index + 1} ya cultiva {GreenhouseCropName(crop)}.", out message);
            greenhouseCrops[index] = crop;
            greenhouseCropProgress[index] = 0;
            return Success($"Invernadero {index + 1}: nuevo cultivo de {GreenhouseCropName(crop)}. El ciclo empieza desde cero.", out message);
        }
        public int WarehouseLevel(int index) => index >= 0 && index < warehouseLevels.Count ? warehouseLevels[index] : 0;
        public IReadOnlyList<CrewMember> Crew => crew;
        public IReadOnlyList<UpgradeTask> Upgrades => upgrades;
        public bool CanRoverReach(Vector2Int cell) => map == null || ColonyLevel == MaxLevel ||
            SpaceFarmMap.CellDistance(map.BaseCell, cell) <= RoverRange;
        public bool HasBarrierAt(Vector2Int cell)
        {
            for (int i = 0; i < barriers.Count; i++)
                if (barriers[i].Cell == cell) return true;
            return false;
        }
        public bool HasDiscovery(int index) => index >= 0 && index < discoveries.Length && discoveries[index];
        public DepositState Deposit(int id) => id >= 0 && id < deposits.Count ? deposits[id] : null;

        public int MineCount => CountWhere(d => d.MineLevel > 0);
        public int BaseOxygenStored { get; private set; }
        public int OxygenLocal(int index) => index == 0 ? BaseOxygenStored : index > 0 && index - 1 < oxygenLocal.Count ? oxygenLocal[index - 1] : 0;
        public int GreenhouseLocal(int index) => index >= 0 && index < greenhouseLocal.Count ? greenhouseLocal[index] : 0;
        public int ExtractorLevel(int id) => Deposit(id)?.ExtractorLevel ?? 0;
        public int ExtractorCount => CountWhere(d => d.Extractor);
        public bool AnySurveyed(DepositKind kind) => CountWhere(d => d.Kind == kind && d.Surveyed) > 0;
        /// <summary>The only possible mine cap is the number of generated iron deposits on this map.</summary>
        public int IronDepositCount => CountWhere(d => d.Kind == DepositKind.Iron);
        public int MaxMines => IronDepositCount;
        public int MineCost => 8 + 4 * MineCount;
        public int ExtractorCost => 6 + 4 * ExtractorCount;

        /// <summary>Minimal layout for rule checks: one iron deposit (id 0), one ice deposit (id 1) and three discoveries.</summary>
        public SpaceFarmState() : this(DefaultDeposits(), new[] { 0, 1, 2 }) { }

        public SpaceFarmState(SpaceFarmMap map) : this(map.Deposits, PointKinds(map))
        {
            this.map = map;
            StartingOxygenCell = map.BaseCell + new Vector2Int(2, 0);
            warehouseCells[0] = map.BaseCell + new Vector2Int(-4, 0);
            eventRandom = new SpaceFarmRandom((ulong)(uint)map.Seed * 104729UL + 320203UL);
        }

        public SpaceFarmState(IReadOnlyList<MapDeposit> layout, IReadOnlyList<int> pointKinds)
        {
            warehouseCells.Add(Vector2Int.zero);
            warehouseLevels.Add(1);
            eventRandom = new SpaceFarmRandom(371921UL);
            crew.Add(new CrewMember { Id = 1 });
            crew.Add(new CrewMember { Id = 2 });
            for (int i = 0; i < layout.Count; i++)
                deposits.Add(new DepositState
                {
                    Id = i,
                    Kind = layout[i].Kind,
                    Distance = layout[i].Distance,
                    Name = string.IsNullOrEmpty(layout[i].Name) ? (layout[i].Kind == DepositKind.Iron ? "Fe-" : "Hielo-") + (i + 1) : layout[i].Name
                });
            discoveryKinds = new int[pointKinds.Count];
            for (int i = 0; i < pointKinds.Count; i++) discoveryKinds[i] = pointKinds[i];
            discoveries = new bool[pointKinds.Count];
        }

        private static List<MapDeposit> DefaultDeposits() => new List<MapDeposit>
        {
            new MapDeposit { Id = 0, Kind = DepositKind.Iron, Distance = 5, Name = "Fe-1" },
            new MapDeposit { Id = 1, Kind = DepositKind.Ice, Distance = 7, Name = "Hielo-1" }
        };

        private static int[] PointKinds(SpaceFarmMap map)
        {
            int[] kinds = new int[map.Points.Count];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = map.Points[i].Kind;
            return kinds;
        }

        private int CountWhere(Func<DepositState, bool> match)
        {
            int count = 0;
            for (int i = 0; i < deposits.Count; i++) if (match(deposits[i])) count++;
            return count;
        }

        private int AddIron(int amount) => AddMaterial(MaterialResource.Iron, amount);

        public int AddMaterial(MaterialResource kind, int amount)
        {
            int current = kind == MaterialResource.Iron ? Iron : kind == MaterialResource.Oxygen ? Oxygen : kind == MaterialResource.Water ? Water : Food;
            // All stored supplies occupy one colony-wide pool. Each warehouse contributes capacity;
            // no resource has an independent 60-unit ceiling.
            int perResourceCapacity = WarehouseCapacity;
            int added = Mathf.Clamp(amount, 0, Mathf.Min(Mathf.Max(0, WarehouseCapacity - WarehouseUsed), Mathf.Max(0, perResourceCapacity - current)));
            switch (kind)
            {
                case MaterialResource.Iron: Iron += added; break;
                case MaterialResource.Oxygen: Oxygen += added; break;
                case MaterialResource.Water: Water += added; break;
                case MaterialResource.Food: Food += added; break;
            }
            return added;
        }

        private static int RepairCost(DamagedStructureKind kind)
        {
            switch (kind)
            {
                case DamagedStructureKind.Solar: return 8;
                case DamagedStructureKind.Dormitory: return 6;
                case DamagedStructureKind.Greenhouse: return 6;
                case DamagedStructureKind.Rover: return 5;
                case DamagedStructureKind.Mine: return 8;
                default: return 0;
            }
        }

        public bool IsDamaged(DamagedStructureKind kind, int target = -1) =>
            DamagedStructure == kind && (target < 0 || DamagedTarget == target);

        public bool RepairDamagedStructure(out string message)
        {
            if (DamagedStructure == DamagedStructureKind.None) return Fail("No hay ninguna instalación averiada.", out message);
            int taskTarget = RepairTaskTarget;
            if (HasUpgrade("repair", taskTarget)) return Fail($"La reparación ya está en curso: {UpgradeRemaining("repair", taskTarget)} s.", out message);
            int ironCost = RepairIronCost;
            int energyCost = RepairEnergyCost;
            if (Iron < ironCost) return Fail($"La reparación necesita {ironCost} Fe.", out message);
            if (Energy < energyCost) return Fail($"La reparación necesita {energyCost} de energía.", out message);
            string name = DamagedStructureName(DamagedStructure, DamagedTarget);
            Iron -= ironCost;
            Energy -= energyCost;
            upgrades.Add(new UpgradeTask { Kind = "repair", Target = taskTarget, Remaining = UpgradeSeconds, Duration = UpgradeSeconds });
            return Success($"Reparación de {name} iniciada: 30 s, -{ironCost} Fe y -{energyCost} energía.", out message);
        }

        public string DamagedStructureName(DamagedStructureKind kind, int target = -1)
        {
            switch (kind)
            {
                case DamagedStructureKind.Solar: return "Paneles solares";
                case DamagedStructureKind.Dormitory: return "Dormitorio";
                case DamagedStructureKind.Greenhouse: return $"Invernadero {target + 1}";
                case DamagedStructureKind.Rover: return "Rover";
                case DamagedStructureKind.Mine: return $"Mina {Deposit(target)?.Name ?? "marciana"}";
                default: return "Instalación";
            }
        }

        private bool RollStorm()
        {
            if (Sol < 3 || Sol - lastStormSol < 2) return false;
            float chance = Mathf.Clamp(.18f + (ColonyLevel - 1) * .06f + Mathf.Max(0, Sol - 6) * .015f, .18f, .55f);
            return eventRandom.Value() < chance;
        }

        private bool TryDamageRandomStructure()
        {
            if (DamagedStructure != DamagedStructureKind.None) return false;
            float chance = Mathf.Clamp(.24f + (ColonyLevel - 1) * .05f + Mathf.Max(0, Sol - 6) * .01f, .24f, .55f);
            if (eventRandom.Value() >= chance) return false;

            List<KeyValuePair<DamagedStructureKind, int>> candidates = new List<KeyValuePair<DamagedStructureKind, int>>
            {
                new KeyValuePair<DamagedStructureKind, int>(DamagedStructureKind.Solar, 0),
                new KeyValuePair<DamagedStructureKind, int>(DamagedStructureKind.Dormitory, 0),
                new KeyValuePair<DamagedStructureKind, int>(DamagedStructureKind.Rover, 0)
            };
            for (int i = 0; i < greenhouseCells.Count; i++) candidates.Add(new KeyValuePair<DamagedStructureKind, int>(DamagedStructureKind.Greenhouse, i));
            for (int i = 0; i < deposits.Count; i++)
                if (deposits[i].MineLevel > 0) candidates.Add(new KeyValuePair<DamagedStructureKind, int>(DamagedStructureKind.Mine, i));
            if (candidates.Count == 0) return false;
            KeyValuePair<DamagedStructureKind, int> chosen = candidates[eventRandom.Range(0, candidates.Count)];
            DamagedStructure = chosen.Key;
            DamagedTarget = chosen.Value;
            return true;
        }

        public int StructureCost(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Mine: return MineCost;
                case StructureKind.Extractor: return ExtractorCost;
                case StructureKind.Greenhouse: return 6;
                case StructureKind.OxygenPlant: return 9;
                case StructureKind.Barrier: return 1;
                case StructureKind.Warehouse: return 12;
                default: return 0;
            }
        }

        /// <summary>Catalog availability, independent of the pointer position.</summary>
        public string PlacementAvailability(StructureKind kind)
        {
            if (kind == StructureKind.Warehouse && WarehouseCount >= MaxWarehouses) return $"Límite de {MaxWarehouses} almacenes";
            if (kind == StructureKind.Greenhouse && greenhouseCells.Count >= 5) return "Límite de 5 invernaderos";
            if (kind == StructureKind.OxygenPlant && OxygenPlants >= 5) return "Límite de 5 plantas de O2";
            if (kind == StructureKind.Mine)
            {
                if (!HasFreeSurveyed(DepositKind.Iron)) return "Estudiá un yacimiento de hierro libre";
            }
            if (kind == StructureKind.Extractor)
            {
                if (ExtractorCount >= MaxExtractors) return "Límite de 5 extractores";
                if (!HasFreeSurveyed(DepositKind.Ice)) return "Estudiá un depósito de hielo libre";
            }
            int missing = StructureCost(kind) - Iron;
            return missing > 0 ? $"Faltan {missing} Fe" : string.Empty;
        }

        private bool HasFreeSurveyed(DepositKind kind)
        {
            for (int i = 0; i < deposits.Count; i++)
                if (deposits[i].Kind == kind && deposits[i].Surveyed && !deposits[i].HasBuilding) return true;
            return false;
        }

        /// <summary>The preview and the final transaction use this exact rule, including current resources.</summary>
        public string PlacementBlocker(StructureKind kind, Vector2Int cell, bool revealed)
        {
            if (kind == StructureKind.Barrier) return BarrierBlocker(cell, revealed);
            if (kind == StructureKind.None) return "Elegí una estructura";
            if (map != null && !map.Inside(cell)) return "Fuera del mapa";
            if (map != null && !CanRoverReach(cell)) return $"Ese sector queda fuera del alcance nivel {ColonyLevel}";
            if (!revealed) return "Sector oculto: exploralo con el rover";
            string availability = PlacementAvailability(kind);
            if (availability.Length > 0) return availability;
            if (HasBarrierInFootprint(cell, 1, 1)) return "No se puede colocar una estructura sobre un muro";
            if (IsCellOccupiedByStructure(cell)) return "Ese sector está ocupado por una estructura";
            foreach (Vector2Int stored in warehouseCells)
                if (Within(cell, stored, 1, 1)) return "Ese sector está ocupado por un almacén";
            if (kind == StructureKind.Mine || kind == StructureKind.Extractor)
            {
                MapDeposit deposit = map?.DepositAt(cell);
                if (deposit == null) return kind == StructureKind.Mine ? "Necesita hierro estudiado" : "Necesita hielo estudiado";
                return kind == StructureKind.Mine ? MineBlocker(deposit.Id) : ExtractorBlocker(deposit.Id);
            }
            if (map == null) return string.Empty; // Small rule-only fixtures have no terrain.
            Vector2Int baseCell = map.BaseCell;
            if (Mathf.Abs(cell.x - baseCell.x) <= 4 && Mathf.Abs(cell.y - baseCell.y) <= 4)
                return "Área reservada del hábitat";
            foreach (MapDeposit deposit in map.Deposits)
                if (Near(cell, deposit.Cell, 2)) return "Demasiado cerca de un yacimiento";
            foreach (MapPoint point in map.Points)
                if (Near(cell, point.Cell, 1)) return "Punto de interés ocupado";
            foreach (Vector2Int placed in greenhouseCells)
                if (Near(cell, placed, 2)) return "Demasiado cerca del invernadero";
            foreach (Vector2Int placed in oxygenCells)
                if (Near(cell, placed, 2)) return "Demasiado cerca de la planta de O2";
            return string.Empty;
        }

        public string EditMoveBlocker(StructureKind kind, int index, Vector2Int cell, bool revealed)
        {
            if (kind != StructureKind.Barrier && kind != StructureKind.Greenhouse && kind != StructureKind.OxygenPlant && kind != StructureKind.Warehouse)
                return "Esta instalación está anclada y no se puede mover";
            if (map != null && !map.Inside(cell)) return "Fuera del mapa";
            if (!revealed) return "Sector oculto: exploralo primero";
            if (kind == StructureKind.Greenhouse && (index < 0 || index >= greenhouseCells.Count)) return "Invernadero inexistente";
            if (kind == StructureKind.OxygenPlant && index != -1 && (index < 0 || index >= oxygenCells.Count)) return "Planta de oxígeno inexistente";
            if (kind == StructureKind.OxygenPlant && index == -1 && OxygenPlants <= 0) return "Planta de oxígeno inexistente";
            if (kind == StructureKind.Warehouse && (index < 0 || index >= warehouseCells.Count)) return "Almacén inexistente";
            if (kind == StructureKind.Barrier)
            {
                if (index < 0 || index >= barriers.Count) return "Muro inexistente";
                if (HasBarrierAt(cell) && barriers[index].Cell != cell) return "Ya hay un muro en ese sector";
                if (map != null && (map.DepositAt(cell) != null || map.PointAt(cell) >= 0)) return "Dejá libre el yacimiento o señal";
                if (IsCellOccupiedByStructure(cell)) return "No se puede colocar un muro sobre una estructura";
            }
            if (HasBarrierInFootprint(cell, 1, 1, kind == StructureKind.Barrier ? index : -1)) return "No se puede superponer con un muro";
            Vector2Int baseCell = map != null ? map.BaseCell : new Vector2Int(-10000, -10000);
            if (Within(cell, baseCell, 4, 4)) return "Área reservada del hábitat";
            if (map != null)
            {
                foreach (MapDeposit deposit in map.Deposits)
                    if (Near(cell, deposit.Cell, 2)) return "Demasiado cerca de un yacimiento";
                foreach (MapPoint point in map.Points)
                    if (Near(cell, point.Cell, 1)) return "Punto de interés ocupado";
            }
            for (int i = 0; i < greenhouseCells.Count; i++)
                if (!(kind == StructureKind.Greenhouse && i == index) && Near(cell, greenhouseCells[i], 2)) return "Demasiado cerca de un invernadero";
            for (int i = 0; i < oxygenCells.Count; i++)
                if (!(kind == StructureKind.OxygenPlant && i == index) && Near(cell, oxygenCells[i], 2)) return "Demasiado cerca de una planta de O2";
            if (map != null && !(kind == StructureKind.OxygenPlant && index == -1) && Near(cell, StartingOxygenCell, 2)) return "Demasiado cerca de una planta de O2";
            for (int i = 0; i < warehouseCells.Count; i++)
                if (!(kind == StructureKind.Warehouse && i == index) && Within(cell, warehouseCells[i], 1, 1)) return "Ese sector está ocupado por un almacén";
            foreach (DepositState deposit in deposits)
                if ((deposit.MineLevel > 0 || deposit.Extractor) && Near(cell, map.Deposits[deposit.Id].Cell, 2)) return "Demasiado cerca de una mina o extractor";
            return string.Empty;
        }

        public bool MoveStructure(StructureKind kind, int index, Vector2Int cell, bool revealed, out string message)
        {
            string blocker = EditMoveBlocker(kind, index, cell, revealed);
            if (blocker.Length > 0) return Fail(blocker + ".", out message);
            if (kind == StructureKind.Barrier) barriers[index].Cell = cell;
            else if (kind == StructureKind.Greenhouse) greenhouseCells[index] = cell;
            else if (kind == StructureKind.OxygenPlant && index == -1) StartingOxygenCell = cell;
            else if (kind == StructureKind.OxygenPlant) oxygenCells[index] = cell;
            else warehouseCells[index] = cell;
            return Success($"{StructureEditName(kind, index)} movida al sector {cell.x + 1},{cell.y + 1}.", out message);
        }

        public string RemovalBlocker(StructureKind kind, int index)
        {
            if (kind == StructureKind.Barrier) return index >= 0 && index < barriers.Count ? string.Empty : "Muro inexistente";
            if (kind == StructureKind.Greenhouse)
            {
                if (index < 0 || index >= greenhouseCells.Count) return "Invernadero inexistente";
                if (greenhouseLocal[index] > 0) return $"El depósito local conserva {greenhouseLocal[index]} comida; enviá el rover primero";
                if (HasUpgrade("greenhouse", index)) return "Esperá a que termine la mejora";
            }
            else if (kind == StructureKind.OxygenPlant)
            {
                if (index < 0 || index >= oxygenCells.Count) return "Planta extra inexistente";
                if (oxygenLocal[index + 1] > 0) return $"El depósito local conserva {oxygenLocal[index + 1]} O2; enviá el rover primero";
                if (HasUpgrade("oxygen", index + 1)) return "Esperá a que termine la mejora";
            }
            else if (kind == StructureKind.Warehouse)
            {
                if (index < 0 || index >= warehouseCells.Count) return "Almacén inexistente";
                if (WarehouseCount <= 1) return "Debe quedar al menos un almacén";
                int capacityAfterRemoval = WarehouseCapacity - (WarehouseStorage + (warehouseLevels[index] - 1) * 40);
                if (WarehouseUsed > capacityAfterRemoval) return $"La colonia guarda {WarehouseUsed} unidades y los almacenes restantes admitirían {capacityAfterRemoval}";
                if (HasUpgrade("warehouse", index)) return "Esperá a que termine la mejora";
            }
            else if (kind == StructureKind.Mine || kind == StructureKind.Extractor)
            {
                DepositState d = Deposit(index);
                if (d == null || kind == StructureKind.Mine && d.MineLevel == 0 || kind == StructureKind.Extractor && !d.Extractor)
                    return "No hay esa instalación en el yacimiento";
                if (kind == StructureKind.Mine && d.MineStored > 0) return $"La mina conserva {d.MineStored} Fe; enviá el rover primero";
                if (kind == StructureKind.Extractor && d.WaterStored > 0) return $"El extractor conserva {d.WaterStored} agua; enviá el rover primero";
                if (HasUpgrade(kind == StructureKind.Mine ? "mine" : "extractor", index)) return "Esperá a que termine la mejora";
            }
            else return "Esta instalación base no se puede eliminar";
            return string.Empty;
        }

        public bool RemoveStructure(StructureKind kind, int index, out string message)
        {
            string blocker = RemovalBlocker(kind, index);
            if (blocker.Length > 0) return Fail(blocker + ".", out message);
            int refund;
            if (kind == StructureKind.Barrier)
            {
                barriers.RemoveAt(index); refund = 0; // Half of a one-iron segment rounds down.
            }
            else if (kind == StructureKind.Greenhouse)
            {
                greenhouseCells.RemoveAt(index); greenhouseLevels.RemoveAt(index); greenhouseLocal.RemoveAt(index);
                greenhouseCrops.RemoveAt(index); greenhouseCropProgress.RemoveAt(index);
                ShiftCrewTargets(CrewJob.Greenhouse, index); ShiftUpgradeTargets("greenhouse", index);
                if (DamagedStructure == DamagedStructureKind.Greenhouse)
                {
                    if (DamagedTarget == index) ClearDamageAndRepair();
                    else if (DamagedTarget > index) DamagedTarget--;
                }
                Greenhouse = greenhouseCells.Count > 0;
                GreenhouseCell = Greenhouse ? (Vector2Int?)greenhouseCells[0] : null;
                refund = 3;
            }
            else if (kind == StructureKind.OxygenPlant)
            {
                int target = index + 1;
                oxygenCells.RemoveAt(index); oxygenLevels.RemoveAt(index); oxygenLocal.RemoveAt(index);
                ShiftCrewTargets(CrewJob.Oxygen, target); ShiftUpgradeTargets("oxygen", target);
                OxygenPlants--; OxygenPlantTwoCell = oxygenCells.Count > 0 ? (Vector2Int?)oxygenCells[0] : null;
                refund = 4;
            }
            else if (kind == StructureKind.Warehouse)
            {
                warehouseCells.RemoveAt(index); warehouseLevels.RemoveAt(index); ShiftUpgradeTargets("warehouse", index); refund = 6;
            }
            else
            {
                DepositState d = Deposit(index);
                if (kind == StructureKind.Mine) { d.MineLevel = 0; d.MineStored = 0; refund = 4; ShiftCrewTargets(CrewJob.Mine, index); ShiftUpgradeTargets("mine", index); }
                else { d.Extractor = false; d.ExtractorLevel = 0; d.WaterStored = 0; refund = 3; ShiftCrewTargets(CrewJob.Extractor, index); ShiftUpgradeTargets("extractor", index); }
            }
            int refundPaid = AddMaterial(MaterialResource.Iron, refund);
            return Success($"{StructureEditName(kind, index)} eliminado. Reembolso: {refundPaid}/{refund} Fe (aprox. 50 % del costo base, limitado por el espacio libre).", out message);
        }

        private void ShiftCrewTargets(CrewJob job, int removedTarget)
        {
            foreach (CrewMember member in crew)
            {
                if (member.Job != job) continue;
                if (member.Target == removedTarget) { member.Job = CrewJob.None; member.Target = -1; }
                else if (member.Target > removedTarget) member.Target--;
            }
        }

        private void ShiftUpgradeTargets(string kind, int removedTarget)
        {
            for (int i = upgrades.Count - 1; i >= 0; i--)
            {
                if (upgrades[i].Kind != kind) continue;
                if (upgrades[i].Target == removedTarget) upgrades.RemoveAt(i);
                else if (upgrades[i].Target > removedTarget) upgrades[i].Target--;
            }
        }

        private void ClearDamageAndRepair()
        {
            DamagedStructure = DamagedStructureKind.None; DamagedTarget = -1;
            for (int i = upgrades.Count - 1; i >= 0; i--) if (upgrades[i].Kind == "repair") upgrades.RemoveAt(i);
        }

        private static string StructureEditName(StructureKind kind, int index)
        {
            switch (kind)
            {
                case StructureKind.Barrier: return "Muro";
                case StructureKind.Greenhouse: return $"Invernadero {index + 1}";
                case StructureKind.OxygenPlant: return $"Planta de oxígeno {index + 2}";
                case StructureKind.Warehouse: return $"Almacén {index + 1}";
                case StructureKind.Mine: return $"Mina Fe-{index + 1}";
                case StructureKind.Extractor: return $"Extractor Hielo-{index + 1}";
                default: return "Instalación";
            }
        }

        private static bool Near(Vector2Int a, Vector2Int b, int radius) =>
            Mathf.Abs(a.x - b.x) <= radius && Mathf.Abs(a.y - b.y) <= radius;

        public bool IsCellOccupiedByStructure(Vector2Int cell)
        {
            if (map == null) return false;
            Vector2Int baseCell = map.BaseCell;
            if (Within(cell, baseCell, 2, 1) ||
                Within(cell, StartingOxygenCell, 1, 1) ||
                Within(cell, baseCell + new Vector2Int(-2, -1), 1, 1) ||
                Within(cell, baseCell + new Vector2Int(-1, 2), 1, 1)) return true;
            foreach (Vector2Int placed in greenhouseCells) if (Within(cell, placed, 1, 1)) return true;
            foreach (Vector2Int placed in oxygenCells) if (Within(cell, placed, 1, 1)) return true;
            if (Within(cell, StartingOxygenCell, 1, 1)) return true;
            foreach (Vector2Int placed in warehouseCells) if (Within(cell, placed, 1, 1)) return true;
            foreach (DepositState deposit in deposits)
                if ((deposit.MineLevel > 0 || deposit.Extractor) && Within(cell, map.Deposits[deposit.Id].Cell, 1, 1)) return true;
            return false;
        }

        private static bool Within(Vector2Int cell, Vector2Int center, int radiusX, int radiusY) =>
            Mathf.Abs(cell.x - center.x) <= radiusX && Mathf.Abs(cell.y - center.y) <= radiusY;

        public bool HasBarrierInFootprint(Vector2Int cell, int radiusX, int radiusY, int excludedIndex = -1)
        {
            for (int i = 0; i < barriers.Count; i++)
                if (i != excludedIndex && Within(cell, barriers[i].Cell, radiusX, radiusY)) return true;
            return false;
        }

        public bool IsCellBlocked(Vector2Int cell)
        {
            if (map != null && !map.Inside(cell)) return true;
            return HasBarrierAt(cell) || IsCellOccupiedByStructure(cell);
        }

        public string BarrierBlocker(Vector2Int cell, bool revealed)
        {
            if (map != null && !map.Inside(cell)) return "Fuera del mapa";
            if (!revealed) return "Sector oculto: exploralo con el rover";
            if (HasBarrierAt(cell)) return "Ya hay un muro en ese sector";
            if (map != null && map.DepositAt(cell) != null) return "Dejá libre el yacimiento para que pueda estudiarlo el rover";
            if (map != null && map.PointAt(cell) >= 0) return "Dejá libre la señal para que pueda recogerla el rover";
            if (IsCellOccupiedByStructure(cell)) return "No se puede poner un muro sobre una estructura";
            if (map != null && Within(cell, map.BaseCell + new Vector2Int(0, -2), 1, 1))
                return "Dejá libre el acceso del rover a la base";
            if (Iron < 1) return "Falta 1 Fe";
            return string.Empty;
        }

        public bool PlaceStructure(StructureKind kind, Vector2Int cell, bool revealed, out string message)
        {
            string blocker = PlacementBlocker(kind, cell, revealed);
            if (blocker.Length > 0) return Fail(blocker + ".", out message);
            if (kind == StructureKind.Mine) return BuildMine(map.DepositAt(cell).Id, out message);
            if (kind == StructureKind.Extractor) return BuildWaterExtractor(map.DepositAt(cell).Id, out message);
            Iron -= StructureCost(kind);
            if (kind == StructureKind.Greenhouse)
            {
                Greenhouse = true;
                greenhouseCells.Add(cell);
                greenhouseLevels.Add(1);
                greenhouseLocal.Add(0);
                greenhouseCrops.Add(GreenhouseCrop.Wheat);
                greenhouseCropProgress.Add(0);
                if (!GreenhouseCell.HasValue) GreenhouseCell = cell;
                GainXp(6);
                return Success("Invernadero listo: empieza con trigo y cosecha 7 de comida cada sol.", out message);
            }
            if (kind == StructureKind.Warehouse)
            {
                warehouseCells.Add(cell);
                warehouseLevels.Add(1);
                GainXp(6);
                return Success($"Almacén construido. Capacidad compartida: {WarehouseCapacity} unidades.", out message);
            }
            OxygenPlants++;
            oxygenCells.Add(cell);
            oxygenLevels.Add(1);
            oxygenLocal.Add(0);
            if (!OxygenPlantTwoCell.HasValue) OxygenPlantTwoCell = cell;
            GainXp(6);
            return Success("Nueva planta de oxígeno lista. Cada planta genera 7 de O2 y consume 2 de energía por sol.", out message);
        }

        public string SurveyDeposit(int id)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null) return "Ahí no hay ningún yacimiento.";
            if (deposit.Surveyed) return $"{deposit.Name} ya está estudiado.";
            // The first sample of each resource is the big one; later surveys bring a single unit.
            int sample = AnySurveyed(deposit.Kind) ? 1 : 2;
            deposit.Surveyed = true;
            GainXp(8);
            if (deposit.Kind == DepositKind.Iron)
            {
                int received = AddIron(sample);
                return LastReport = received == sample
                    ? $"Rover: yacimiento {deposit.Name} confirmado. Dejó {received} Fe de muestra en el almacén. Seleccionalo para construir una mina."
                    : $"Rover: {deposit.Name} confirmado. El almacén solo tenía espacio para {received} Fe de muestra.";
            }
            int waterSample = AddMaterial(MaterialResource.Water, sample);
            return LastReport = $"Rover: depósito {deposit.Name} confirmado. Guardó {waterSample}/{sample} de agua de muestra en la colonia. Seleccionalo para construir un extractor.";
        }

        /// <summary>Why a mine cannot be built there right now, or an empty string.</summary>
        public string MineBlocker(int id)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null || deposit.Kind != DepositKind.Iron) return "Elegí un yacimiento de hierro";
            if (!deposit.Surveyed) return "Primero estudiá ese yacimiento con el rover";
            if (deposit.MineLevel > 0) return "Ese yacimiento ya tiene mina";
            if (Iron < MineCost) return $"La mina necesita {MineCost} de hierro";
            return string.Empty;
        }

        public bool BuildMine(int id, out string message)
        {
            string blocker = MineBlocker(id);
            if (blocker.Length > 0) return Fail(blocker + ".", out message);
            DepositState deposit = Deposit(id);
            Iron -= MineCost;
            deposit.MineLevel = 1;
            deposit.LastResult = new SystemResult();
            GainXp(8);
            return Success($"Mina nivel 1 construida en {deposit.Name}. Produce hasta 5 Fe por sol, usa 2 de energía y almacena 18. Enviá el rover para recoger.", out message);
        }

        public bool UpgradeMine(int id, out string message)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null || deposit.MineLevel == 0) return Fail("Construí primero la mina.", out message);
            if (deposit.MineLevel >= MaxLevel) return Fail("La mina está en nivel máximo.", out message);
            if (HasUpgrade("mine", id)) return Fail("Esa mina ya se está mejorando.", out message);
            int cost = MineUpgradeCost(id);
            int duration = UpgradeSecondsForNextLevel("mine", id);
            if (Iron < cost) return Fail($"La mejora necesita {cost} de hierro.", out message);
            Iron -= cost;
            upgrades.Add(new UpgradeTask { Kind = "mine", Target = id, Remaining = duration, Duration = duration });
            return Success($"Mejora de {deposit.Name} iniciada: {duration} segundos, {cost} Fe.", out message);
        }

        public bool UpgradeRover(out string message)
        {
            if (RoverLevel >= 2) return Fail("El nivel 2 es el máximo de esta demo.", out message);
            if (HasUpgrade("rover", 0)) return Fail("El rover ya se está mejorando.", out message);
            int cost = RoverUpgradeCost;
            int duration = UpgradeSecondsForNextLevel("rover", 0);
            if (Iron < cost) return Fail($"El rover necesita {cost} de hierro para la mejora.", out message);
            Iron -= cost;
            upgrades.Add(new UpgradeTask { Kind = "rover", Remaining = duration, Duration = duration });
            return Success($"Mejora del rover iniciada: {duration} segundos, {cost} Fe; tendrá más capacidad y visión.", out message);
        }

        public bool UpgradeSolar(out string message)
        {
            if (SolarLevel >= MaxLevel) return Fail("Los paneles están en nivel máximo.", out message);
            if (HasUpgrade("solar", 0)) return Fail("Los paneles ya se están mejorando.", out message);
            int cost = SolarUpgradeCost;
            int duration = UpgradeSecondsForNextLevel("solar", 0);
            if (Iron < cost) return Fail($"La mejora solar necesita {cost} Fe.", out message);
            Iron -= cost;
            upgrades.Add(new UpgradeTask { Kind = "solar", Remaining = duration, Duration = duration });
            return Success($"Ampliación solar iniciada: {duration} segundos. Costo: {cost} Fe.", out message);
        }

        public bool UpgradeExtractor(int id, out string message) => BeginStructureUpgrade("extractor", id, ExtractorUpgradeCost(id), ExtractorLevel(id), out message);
        public bool UpgradeGreenhouse(int id, out string message) => BeginStructureUpgrade("greenhouse", id, GreenhouseUpgradeCost(id), GreenhouseLevel(id), out message);
        public bool UpgradeOxygen(int id, out string message) => BeginStructureUpgrade("oxygen", id, OxygenUpgradeCost(id), OxygenLevel(id), out message);
        public bool UpgradeWarehouse(int id, out string message) => BeginStructureUpgrade("warehouse", id, WarehouseUpgradeCost(id), WarehouseLevel(id), out message);

        private bool BeginStructureUpgrade(string kind, int target, int cost, int level, out string message)
        {
            if (level <= 0) return Fail("Esa instalación todavía no existe.", out message);
            if (level >= MaxLevel) return Fail("La instalación está en nivel máximo.", out message);
            if (HasUpgrade(kind, target)) return Fail("Esa instalación ya se está mejorando.", out message);
            int duration = UpgradeSecondsForNextLevel(kind, target);
            if (Iron < cost) return Fail($"La mejora necesita {cost} Fe.", out message);
            Iron -= cost;
            upgrades.Add(new UpgradeTask { Kind = kind, Target = target, Remaining = duration, Duration = duration });
            return Success($"Mejora de {kind} {target + 1} iniciada: nivel {level + 1}, {duration} s, {cost} Fe.", out message);
        }

        public int ExtractorUpgradeCost(int id) => 7 + Mathf.Max(0, ExtractorLevel(id) - 1) * 2;
        public int GreenhouseUpgradeCost(int id) => 7 + Mathf.Max(0, GreenhouseLevel(id) - 1) * 2;
        public int OxygenUpgradeCost(int id) => 7 + Mathf.Max(0, OxygenLevel(id) - 1) * 2;
        public int WarehouseUpgradeCost(int id) => 8 + Mathf.Max(0, WarehouseLevel(id) - 1) * 2;

        public int MineUpgradeCost(int id)
        {
            DepositState deposit = Deposit(id);
            return 6 + Mathf.Max(0, (deposit?.MineLevel ?? 1) - 1) * 2;
        }

        public int RoverUpgradeCost => 5 + Mathf.Max(0, RoverLevel - 1) * 2;
        public int SolarUpgradeCost => 8 + Mathf.Max(0, SolarLevel - 1) * 2;

        public int UpgradeSecondsForNextLevel(string kind, int target)
        {
            int level = 1;
            if (kind == "mine") level = Deposit(target)?.MineLevel ?? 1;
            else if (kind == "rover") level = RoverLevel;
            else if (kind == "solar") level = SolarLevel;
            else if (kind == "extractor") level = ExtractorLevel(target);
            else if (kind == "greenhouse") level = GreenhouseLevel(target);
            else if (kind == "oxygen") level = OxygenLevel(target);
            else if (kind == "warehouse") level = WarehouseLevel(target);
            return Mathf.RoundToInt(UpgradeSeconds * Mathf.Pow(2f, Mathf.Max(0, level - 1)));
        }

        public int UpgradeSecondsToDisplay(string kind, int target) =>
            HasUpgrade(kind, target) ? UpgradeRemaining(kind, target) : UpgradeSecondsForNextLevel(kind, target);
        public bool HasUpgrade(string kind, int target)
        {
            foreach (UpgradeTask task in upgrades)
                if (task.Kind == kind && task.Target == target) return true;
            return false;
        }

        public int UpgradeRemaining(string kind, int target)
        {
            foreach (UpgradeTask task in upgrades)
                if (task.Kind == kind && task.Target == target) return Mathf.CeilToInt(task.Remaining);
            return 0;
        }

        public bool Tick(float seconds)
        {
            bool changed = false;
            for (int i = upgrades.Count - 1; i >= 0; i--)
            {
                UpgradeTask task = upgrades[i];
                task.Remaining -= Mathf.Max(0, seconds);
                if (task.Remaining > 0) continue;
                if (task.Kind == "mine") Deposit(task.Target).MineLevel = Mathf.Min(MaxLevel, Deposit(task.Target).MineLevel + 1);
                else if (task.Kind == "rover") RoverLevel = 2;
                else if (task.Kind == "solar") SolarLevel++;
                else if (task.Kind == "extractor") Deposit(task.Target).ExtractorLevel++;
                else if (task.Kind == "greenhouse") greenhouseLevels[task.Target]++;
                else if (task.Kind == "oxygen") { if (task.Target == 0) BaseOxygenLevel++; else oxygenLevels[task.Target - 1]++; }
                else if (task.Kind == "warehouse") warehouseLevels[task.Target]++;
                else if (task.Kind == "repair")
                {
                    string repaired = DamagedStructureName(DamagedStructure, DamagedTarget);
                    DamagedStructure = DamagedStructureKind.None;
                    DamagedTarget = -1;
                    upgrades.RemoveAt(i);
                    LastReport = $"Reparación de {repaired} completada.";
                    changed = true;
                    continue;
                }
                upgrades.RemoveAt(i);
                GainXp(6);
                LastReport = $"Mejora de {task.Kind} completada.";
                changed = true;
            }
            foreach (CrewMember member in crew)
            {
                if (member.Resting)
                {
                    float recovery = IsDamaged(DamagedStructureKind.Dormitory) ? 1.25f : 2.5f;
                    member.Energy = Mathf.Min(100f, member.Energy + seconds * recovery);
                    if (member.Energy >= 95f) { member.Resting = false; changed = true; }
                }
                else if (member.Job != CrewJob.None)
                {
                    member.Energy = Mathf.Max(0f, member.Energy - seconds * .9f);
                    if (member.Energy <= 15f) { member.Resting = true; changed = true; }
                }
            }
            return changed;
        }

        public bool AssignWorker(CrewJob job, int target, out string message)
        {
            if (!ValidWorkplace(job, target, out message)) return false;
            foreach (CrewMember member in crew)
                if (member.Job == job && member.Target == target)
                    return AssignCrew(member.Id, CrewJob.None, -1, out message);
            foreach (CrewMember member in crew)
                if (member.Job == CrewJob.None)
                    return AssignCrew(member.Id, job, target, out message);
            return Fail("No hay astronautas libres. Desasigná uno desde su instalación.", out message);
        }

        public bool AssignCrew(int memberId, CrewJob job, int target, out string message)
        {
            CrewMember chosen = null;
            foreach (CrewMember member in crew)
                if (member.Id == memberId) { chosen = member; break; }
            if (chosen == null) return Fail("Astronauta inexistente.", out message);
            if (job == CrewJob.None)
            {
                chosen.Job = CrewJob.None;
                chosen.Target = -1;
                return Success($"Astronauta {chosen.Id} vuelve al hábitat.", out message);
            }
            if (!ValidWorkplace(job, target, out message)) return false;
            CrewMember occupant = WorkerOn(job, target);
            if (occupant != null && occupant != chosen)
                return Fail($"La instalación ya tiene al astronauta {occupant.Id}.", out message);
            chosen.Job = job;
            chosen.Target = target;
            return Success($"Astronauta {chosen.Id} asignado a {JobName(job)}. Bono: +{WorkerBonus(job)} por sol mientras esté despierto.", out message);
        }

        private bool ValidWorkplace(CrewJob job, int target, out string message)
        {
            if (job == CrewJob.None) return Fail("Elegí una instalación.", out message);
            if (job == CrewJob.Mine && (Deposit(target) == null || Deposit(target).MineLevel == 0)) return Fail("Elegí una mina construida.", out message);
            if (job == CrewJob.Extractor && (Deposit(target) == null || !Deposit(target).Extractor)) return Fail("Elegí un extractor construido.", out message);
            if (job == CrewJob.Greenhouse && (target < 0 || target >= greenhouseCells.Count)) return Fail("Construí un invernadero.", out message);
            if (job == CrewJob.Oxygen && (target < 0 || target >= OxygenPlants)) return Fail("Construí una planta de O2.", out message);
            if (job == CrewJob.Solar && target != 0) return Fail("Paneles solares no disponibles.", out message);
            message = string.Empty;
            return true;
        }

        private static string JobName(CrewJob job)
        {
            switch (job)
            {
                case CrewJob.Mine: return "la mina";
                case CrewJob.Extractor: return "el extractor";
                case CrewJob.Greenhouse: return "el invernadero";
                case CrewJob.Oxygen: return "la planta de oxígeno";
                case CrewJob.Solar: return "los paneles solares";
                default: return "una instalación";
            }
        }

        public int ActiveWorkers(CrewJob job, int target)
        {
            int count = 0;
            foreach (CrewMember member in crew)
                if (member.Job == job && member.Target == target && !member.Resting && member.Energy > 15f) count++;
            return count;
        }

        public CrewMember WorkerOn(CrewJob job, int target)
        {
            if (job == CrewJob.None) return null;
            foreach (CrewMember member in crew)
                if (member.Job == job && member.Target == target) return member;
            return null;
        }

        public int FreeWorkers
        {
            get { int count = 0; foreach (CrewMember member in crew) if (member.Job == CrewJob.None) count++; return count; }
        }

        public void GainXp(int amount)
        {
            Xp += Mathf.Max(0, amount);
            while (ColonyLevel < MaxLevel && Xp >= LevelXp[ColonyLevel + 1])
            {
                ColonyLevel++;
                for (int i = 0; i < 2; i++) crew.Add(new CrewMember { Id = crew.Count + 1 });
                Population = crew.Count;
                LastReport = $"Colonia nivel {ColonyLevel}: +2 astronautas, hábitat para {HabitatCapacity} y alcance del rover {RoverRange} sectores. Reforzá oxígeno, comida y energía.";
            }
        }

        public string ExtractorBlocker(int id)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null || deposit.Kind != DepositKind.Ice) return "Elegí un depósito de hielo";
            if (!deposit.Surveyed) return "Primero estudiá ese depósito con el rover";
            if (deposit.Extractor) return "Ese depósito ya tiene extractor";
            if (ExtractorCount >= MaxExtractors) return $"La demo admite {MaxExtractors} extractores";
            if (Iron < ExtractorCost) return $"El extractor necesita {ExtractorCost} de hierro";
            return string.Empty;
        }

        public bool BuildWaterExtractor(int id, out string message)
        {
            string blocker = ExtractorBlocker(id);
            if (blocker.Length > 0) return Fail(blocker + ".", out message);
            DepositState deposit = Deposit(id);
            Iron -= ExtractorCost;
            deposit.Extractor = true;
            deposit.ExtractorLevel = 1;
            deposit.LastResult = new SystemResult();
            GainXp(8);
            return Success($"Extractor listo en {deposit.Name}: obtiene 8 de agua por sol a cambio de 2 de energía.", out message);
        }

        public bool BuildBarrierSegment(Vector2Int cell, bool vertical, bool revealed, out string message)
        {
            string blocker = BarrierBlocker(cell, revealed);
            if (blocker.Length > 0) return Fail(blocker + ".", out message);
            Iron--;
            barriers.Add(new BarrierState { Cell = cell, Vertical = vertical });
            return Success($"Muro instalado en {cell.x + 1},{cell.y + 1}. Muros colocados: {BarrierCount}.", out message);
        }

        public bool LoadMineToRover(int id, out string message)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null || deposit.MineLevel == 0) return Fail("Ahí no hay una mina.", out message);
            return LoadResourceToRover(MaterialResource.Iron, id, out message);
        }

        public int LocalStored(MaterialResource kind, int source)
        {
            switch (kind)
            {
                case MaterialResource.Iron: return Deposit(source)?.MineStored ?? 0;
                case MaterialResource.Oxygen: return OxygenLocal(source);
                case MaterialResource.Water: return Deposit(source)?.WaterStored ?? 0;
                case MaterialResource.Food: return GreenhouseLocal(source);
                default: return 0;
            }
        }

        public int LocalSourceCount(MaterialResource kind)
        {
            switch (kind)
            {
                case MaterialResource.Iron:
                case MaterialResource.Water: return deposits.Count;
                case MaterialResource.Oxygen: return OxygenPlants;
                case MaterialResource.Food: return greenhouseCells.Count;
                default: return 0;
            }
        }

        public bool TryGetBestPickup(out MaterialResource kind, out int source)
        {
            kind = MaterialResource.Iron; source = -1;
            if (RoverCargoTotal > 0 || WarehouseFree <= 0) return false;
            MaterialResource[] vital = { MaterialResource.Oxygen, MaterialResource.Water, MaterialResource.Food };
            float lowestRatio = float.MaxValue;
            bool foundVital = false;
            foreach (MaterialResource candidate in vital)
            {
                int reserve = candidate == MaterialResource.Oxygen ? Oxygen : candidate == MaterialResource.Water ? Water : Food;
                float ratio = reserve / (float)Mathf.Max(1, Population * 5);
                for (int i = 0; i < LocalSourceCount(candidate); i++)
                {
                    if (LocalStored(candidate, i) <= 0) continue;
                    if (ratio < lowestRatio) { lowestRatio = ratio; kind = candidate; source = i; foundVital = true; }
                }
            }
            // Protect critically low reserves first, but do not starve iron collection just
            // because a noncritical food/water/oxygen source has stock waiting.
            if (foundVital && lowestRatio < 1f) return true;
            for (int i = 0; i < deposits.Count; i++)
                if (deposits[i].MineLevel > 0 && deposits[i].MineStored > 0)
                { kind = MaterialResource.Iron; source = i; return true; }
            return foundVital;
        }

        public bool LoadResourceToRover(MaterialResource kind, int source, out string message)
        {
            if (RoverCargoTotal >= RoverCapacity) return Fail("La carga del rover está llena. Descargala antes de recoger más.", out message);
            int stored = LocalStored(kind, source);
            if (stored <= 0) return Fail($"No hay {MaterialName(kind)} disponible en ese depósito local.", out message);
            int loaded = Mathf.Min(stored, RoverCapacity - RoverCargoTotal);
            switch (kind)
            {
                case MaterialResource.Iron: Deposit(source).MineStored -= loaded; RoverCargo += loaded; break;
                case MaterialResource.Oxygen:
                    if (source == 0) BaseOxygenStored -= loaded; else oxygenLocal[source - 1] -= loaded;
                    RoverOxygenCargo += loaded; break;
                case MaterialResource.Water: Deposit(source).WaterStored -= loaded; RoverWaterCargo += loaded; break;
                case MaterialResource.Food: greenhouseLocal[source] -= loaded; RoverFoodCargo += loaded; break;
            }
            return Success($"Rover recogió {loaded} de {MaterialName(kind)} ({RoverCargoTotal}/{RoverCapacity} unidades de carga).", out message);
        }

        private static string MaterialName(MaterialResource kind)
        {
            switch (kind)
            {
                case MaterialResource.Iron: return "hierro";
                case MaterialResource.Oxygen: return "oxígeno";
                case MaterialResource.Water: return "agua";
                default: return "comida";
            }
        }

        public SaveSnapshot CaptureSave()
        {
            SaveSnapshot snapshot = new SaveSnapshot
            {
                Sol = Sol, Iron = Iron, Energy = Energy, Oxygen = Oxygen, Water = Water, Food = Food,
                Population = Population, ColonyLevel = ColonyLevel, Xp = Xp, Health = Health,
                SolarLevel = SolarLevel, BaseOxygenLevel = BaseOxygenLevel, OxygenPlants = OxygenPlants,
                BaseOxygenStored = BaseOxygenStored, RoverLevel = RoverLevel,
                RoverCargo = RoverCargo, RoverOxygenCargo = RoverOxygenCargo, RoverWaterCargo = RoverWaterCargo,
                RoverFoodCargo = RoverFoodCargo, ShipmentArrivalSol = ShipmentArrivalSol,
                ShipmentFoodRemaining = ShipmentFoodRemaining, ShipmentWaterRemaining = ShipmentWaterRemaining,
                AutomaticCollection = AutomaticCollection,
                DamagedKind = (int)DamagedStructure, DamagedTarget = DamagedTarget, LastStormSol = lastStormSol,
                Discoveries = new int[discoveries.Length], OxygenLevels = oxygenLevels.ToArray(), GreenhouseLevels = greenhouseLevels.ToArray(),
                WarehouseLevels = warehouseLevels.ToArray(), OxygenLocal = oxygenLocal.ToArray(), GreenhouseLocal = greenhouseLocal.ToArray(),
                GreenhouseCrops = CropValues(greenhouseCrops), GreenhouseCropProgress = greenhouseCropProgress.ToArray(),
                HasStartingOxygenCell = true,
                StartingOxygenCell = new CellSnapshot { X = StartingOxygenCell.x, Y = StartingOxygenCell.y },
                Deposits = new DepositSnapshot[deposits.Count],
                Crew = new CrewSnapshot[crew.Count], Upgrades = new UpgradeSnapshot[upgrades.Count],
                Barriers = new CellSnapshot[barriers.Count], Greenhouses = Cells(greenhouseCells),
                OxygenCells = Cells(oxygenCells), WarehouseCells = Cells(warehouseCells),
                LastReport = LastReport, EventRandomState = eventRandom.State
            };
            for (int i = 0; i < discoveries.Length; i++) snapshot.Discoveries[i] = discoveries[i] ? 1 : 0;
            for (int i = 0; i < deposits.Count; i++)
            {
                DepositState d = deposits[i];
                snapshot.Deposits[i] = new DepositSnapshot { Surveyed = d.Surveyed ? 1 : 0, MineLevel = d.MineLevel,
                    MineStored = d.MineStored, Extractor = d.Extractor ? 1 : 0, ExtractorLevel = d.ExtractorLevel,
                    WaterStored = d.WaterStored };
            }
            for (int i = 0; i < crew.Count; i++)
                snapshot.Crew[i] = new CrewSnapshot { Id = crew[i].Id, Job = (int)crew[i].Job, Target = crew[i].Target,
                    Energy = crew[i].Energy, Resting = crew[i].Resting };
            for (int i = 0; i < upgrades.Count; i++)
                snapshot.Upgrades[i] = new UpgradeSnapshot { Kind = upgrades[i].Kind, Target = upgrades[i].Target,
                    Remaining = upgrades[i].Remaining, Duration = upgrades[i].Duration };
            for (int i = 0; i < barriers.Count; i++)
                snapshot.Barriers[i] = new CellSnapshot { X = barriers[i].Cell.x, Y = barriers[i].Cell.y, Vertical = barriers[i].Vertical };
            return snapshot;
        }

        private static CellSnapshot[] Cells(List<Vector2Int> cells)
        {
            CellSnapshot[] result = new CellSnapshot[cells.Count];
            for (int i = 0; i < cells.Count; i++) result[i] = new CellSnapshot { X = cells[i].x, Y = cells[i].y };
            return result;
        }

        public bool RestoreSave(SaveSnapshot snapshot, out string error)
        {
            if (snapshot == null || snapshot.Deposits == null || snapshot.Deposits.Length != deposits.Count ||
                snapshot.Discoveries == null || snapshot.Discoveries.Length != discoveries.Length)
            { error = "El guardado no corresponde al mapa o a esta versión."; return false; }
            Sol = Mathf.Max(1, snapshot.Sol); Iron = Mathf.Max(0, snapshot.Iron); Energy = Mathf.Max(0, snapshot.Energy);
            Oxygen = Mathf.Max(0, snapshot.Oxygen); Water = Mathf.Max(0, snapshot.Water); Food = Mathf.Max(0, snapshot.Food);
            ColonyLevel = Mathf.Clamp(snapshot.ColonyLevel, 1, MaxLevel); Xp = Mathf.Max(0, snapshot.Xp);
            Health = Mathf.Clamp(snapshot.Health, 0, 3); SolarLevel = Mathf.Clamp(snapshot.SolarLevel, 1, MaxLevel);
            BaseOxygenLevel = Mathf.Clamp(snapshot.BaseOxygenLevel, 1, MaxLevel);
            BaseOxygenStored = Mathf.Max(0, snapshot.BaseOxygenStored);
            OxygenPlants = Mathf.Max(1, snapshot.OxygenPlants); RoverLevel = Mathf.Clamp(snapshot.RoverLevel, 1, 2);
            RoverCargo = Mathf.Max(0, snapshot.RoverCargo); RoverOxygenCargo = Mathf.Max(0, snapshot.RoverOxygenCargo);
            RoverWaterCargo = Mathf.Max(0, snapshot.RoverWaterCargo); RoverFoodCargo = Mathf.Max(0, snapshot.RoverFoodCargo);
            ShipmentArrivalSol = Mathf.Max(0, snapshot.ShipmentArrivalSol);
            ShipmentFoodRemaining = Mathf.Max(0, snapshot.ShipmentFoodRemaining);
            ShipmentWaterRemaining = Mathf.Max(0, snapshot.ShipmentWaterRemaining);
            AutomaticCollection = snapshot.AutomaticCollection;
            DamagedStructure = (DamagedStructureKind)Mathf.Clamp(snapshot.DamagedKind, 0, (int)DamagedStructureKind.Mine);
            DamagedTarget = snapshot.DamagedTarget; lastStormSol = snapshot.LastStormSol;
            for (int i = 0; i < discoveries.Length; i++) discoveries[i] = snapshot.Discoveries[i] != 0;
            for (int i = 0; i < deposits.Count; i++)
            {
                deposits[i].Surveyed = snapshot.Deposits[i].Surveyed != 0; deposits[i].MineLevel = Mathf.Clamp(snapshot.Deposits[i].MineLevel, 0, MaxLevel);
                deposits[i].MineStored = Mathf.Max(0, snapshot.Deposits[i].MineStored); deposits[i].Extractor = snapshot.Deposits[i].Extractor != 0;
                deposits[i].ExtractorLevel = deposits[i].Extractor ? Mathf.Clamp(snapshot.Deposits[i].ExtractorLevel, 1, MaxLevel) : 0;
                deposits[i].WaterStored = Mathf.Max(0, snapshot.Deposits[i].WaterStored);
                deposits[i].LastResult = new SystemResult();
            }
            barriers.Clear();
            if (snapshot.Barriers != null) foreach (CellSnapshot b in snapshot.Barriers)
                barriers.Add(new BarrierState { Cell = new Vector2Int(b.X, b.Y), Vertical = b.Vertical });
            ReplaceCells(greenhouseCells, snapshot.Greenhouses); ReplaceCells(oxygenCells, snapshot.OxygenCells);
            ReplaceCells(warehouseCells, snapshot.WarehouseCells);
            StartingOxygenCell = snapshot.HasStartingOxygenCell
                ? new Vector2Int(snapshot.StartingOxygenCell.X, snapshot.StartingOxygenCell.Y)
                : map != null ? map.BaseCell + new Vector2Int(2, 0) : Vector2Int.zero;
            ReplaceInts(oxygenLevels, snapshot.OxygenLevels, Mathf.Max(0, OxygenPlants - 1), 1);
            ReplaceInts(greenhouseLevels, snapshot.GreenhouseLevels, greenhouseCells.Count, 1);
            ReplaceGreenhouseCrops(greenhouseCrops, snapshot.GreenhouseCrops, greenhouseCells.Count);
            ReplaceInts(greenhouseCropProgress, snapshot.GreenhouseCropProgress, greenhouseCells.Count, 0);
            for (int i = 0; i < greenhouseCropProgress.Count; i++)
            {
                if (GreenhouseCropUnlockLevel(greenhouseCrops[i]) > GreenhouseLevel(i)) greenhouseCrops[i] = GreenhouseCrop.Wheat;
                greenhouseCropProgress[i] = Mathf.Clamp(greenhouseCropProgress[i], 0, GreenhouseCropDuration(greenhouseCrops[i]) - 1);
            }
            ReplaceInts(warehouseLevels, snapshot.WarehouseLevels, warehouseCells.Count, 1);
            ReplaceInts(oxygenLocal, snapshot.OxygenLocal, Mathf.Max(0, OxygenPlants - 1), 0);
            ReplaceInts(greenhouseLocal, snapshot.GreenhouseLocal, greenhouseCells.Count, 0);
            Greenhouse = greenhouseCells.Count > 0; GreenhouseCell = FirstOrNull(greenhouseCells);
            OxygenPlantTwoCell = oxygenCells.Count > 0 ? (Vector2Int?)oxygenCells[0] : null;
            crew.Clear();
            if (snapshot.Crew != null) foreach (CrewSnapshot c in snapshot.Crew)
                crew.Add(new CrewMember { Id = c.Id, Job = (CrewJob)Mathf.Clamp(c.Job, 0, (int)CrewJob.Solar), Target = c.Target,
                    Energy = Mathf.Clamp(c.Energy, 0, 100), Resting = c.Resting });
            Population = Mathf.Max(1, crew.Count);
            upgrades.Clear();
            if (snapshot.Upgrades != null) foreach (UpgradeSnapshot u in snapshot.Upgrades)
                upgrades.Add(new UpgradeTask { Kind = u.Kind, Target = u.Target, Remaining = Mathf.Max(0, u.Remaining), Duration = Mathf.Max(1, u.Duration) });
            LastReport = string.IsNullOrEmpty(snapshot.LastReport) ? "Partida cargada." : snapshot.LastReport;
            LastSol = new SolResults(); eventRandom.State = snapshot.EventRandomState;
            error = string.Empty;
            return true;
        }

        private static Vector2Int? FirstOrNull(List<Vector2Int> cells) => cells.Count == 0 ? (Vector2Int?)null : cells[0];
        private static void ReplaceCells(List<Vector2Int> destination, CellSnapshot[] source)
        {
            destination.Clear();
            if (source != null) foreach (CellSnapshot cell in source) destination.Add(new Vector2Int(cell.X, cell.Y));
        }
        private static void ReplaceInts(List<int> destination, int[] source, int count, int fallback)
        {
            destination.Clear();
            for (int i = 0; i < count; i++) destination.Add(source != null && i < source.Length ? Mathf.Max(0, source[i]) : fallback);
        }
        private static int[] CropValues(List<GreenhouseCrop> source)
        {
            int[] values = new int[source.Count];
            for (int i = 0; i < source.Count; i++) values[i] = (int)source[i];
            return values;
        }
        private static void ReplaceGreenhouseCrops(List<GreenhouseCrop> destination, int[] source, int count)
        {
            destination.Clear();
            for (int i = 0; i < count; i++)
            {
                int value = source != null && i < source.Length ? source[i] : (int)GreenhouseCrop.Wheat;
                destination.Add((GreenhouseCrop)Mathf.Clamp(value, (int)GreenhouseCrop.Wheat, (int)GreenhouseCrop.Corn));
            }
        }

        public bool UnloadRover(out string message)
        {
            if (RoverCargoTotal == 0) return Fail("El rover no lleva carga.", out message);
            int before = RoverCargoTotal;
            int iron = AddMaterial(MaterialResource.Iron, RoverCargo); RoverCargo -= iron;
            int oxygen = AddMaterial(MaterialResource.Oxygen, RoverOxygenCargo); RoverOxygenCargo -= oxygen;
            int water = AddMaterial(MaterialResource.Water, RoverWaterCargo); RoverWaterCargo -= water;
            int food = AddMaterial(MaterialResource.Food, RoverFoodCargo); RoverFoodCargo -= food;
            int delivered = before - RoverCargoTotal;
            if (delivered == 0) return Fail("El almacén compartido está lleno. La carga permanece en el rover.", out message);
            GainXp(2);
            string detail = $"+{iron} Fe, +{oxygen} O2, +{water} agua, +{food} comida";
            return Success(RoverCargoTotal == 0
                ? $"Rover descargó {detail}. Almacén {WarehouseUsed}/{WarehouseCapacity}."
                : $"Rover descargó parcialmente {detail}. Conserva {RoverCargoTotal}/{RoverCapacity} unidades; no se perdió carga.", out message);
        }

        public string ClaimDiscovery(int index)
        {
            if (index < 0 || index >= discoveries.Length || discoveries[index])
                return "Ese punto ya fue explorado.";
            discoveries[index] = true;
            GainXp(12);
            switch (discoveryKinds[index])
            {
                case 0: return LastReport = $"Cápsula científica: +{AddIron(2)} Fe guardado en el almacén.";
                case 1: return LastReport = $"Hielo superficial: +{AddMaterial(MaterialResource.Water, 3)} agua entregada al almacén.";
                case 2: Energy = Mathf.Min(EnergyCapacity, Energy + 4); return LastReport = "Batería abandonada: +4 energía recuperada.";
                default: return LastReport = $"Contenedor de víveres: +{AddMaterial(MaterialResource.Food, 3)} comida entregada al almacén.";
            }
        }

        public bool AddColonist(out string message)
        {
            return Fail("La tripulación aumenta de dos en dos al subir el nivel de la colonia.", out message);
        }

        public bool OrderFromEarth(out string message)
        {
            if (ShipmentArrivalSol != 0) return Fail("Ya hay un pedido en camino.", out message);
            if (Iron < 4) return Fail("El pedido a la Tierra cuesta 4 de hierro refinado.", out message);
            Iron -= 4;
            ShipmentArrivalSol = Sol + 2;
            ShipmentFoodRemaining = 5;
            ShipmentWaterRemaining = 5;
            return Success($"Pedido enviado: llegará en el sol {ShipmentArrivalSol} con comida y agua.", out message);
        }

        public string AdvanceSol()
        {
            if (GameOver) return LastReport = "La misión terminó. Abrí el menú de pausa para reiniciar.";
            Sol++;
            GainXp(4);
            bool storm = RollStorm();
            if (storm) lastStormSol = Sol;
            bool damagedThisSol = storm && TryDamageRandomStructure();
            LastSol = new SolResults { Storm = storm };
            if (damagedThisSol)
            {
                LastSol.DamagedStructure = DamagedStructure;
                LastSol.DamagedTarget = DamagedTarget;
            }
            int solarNominal = IsDamaged(DamagedStructureKind.Solar) ? 0 : SolarPerSol + ActiveWorkers(CrewJob.Solar, 0) * WorkerBonus(CrewJob.Solar);
            int solar = IsDamaged(DamagedStructureKind.Solar) ? 0 : storm ? Mathf.Max(3, solarNominal / 3) : solarNominal;
            LastSol.Solar.Nominal = solarNominal;
            LastSol.Solar.Actual = Mathf.Min(solar, EnergyCapacity - Energy);
            LastSol.Solar.Reason = IsDamaged(DamagedStructureKind.Solar) ? "Paneles averiados: reparación necesaria." : storm ? "Tormenta de polvo: llega menos luz." :
                LastSol.Solar.Actual < solar ? "Batería llena." : "Operativa.";
            Energy += LastSol.Solar.Actual;
            string report = $"Sol {Sol}: los paneles generaron {LastSol.Solar.Actual} de energía";
            if (storm) report += " por la tormenta de polvo";
            report += ". ";
            if (damagedThisSol) report += $"La tormenta averió {DamagedStructureName(DamagedStructure, DamagedTarget)}. Reparación: {RepairIronCost} Fe y {RepairEnergyCost} energía. ";

            int oxygenMade = 0;
            for (int i = 0; i < OxygenPlants; i++)
            {
                SystemResult plant = new SystemResult { Nominal = 7 + 3 * (OxygenLevel(i) - 1) + ActiveWorkers(CrewJob.Oxygen, i) * WorkerBonus(CrewJob.Oxygen) };
                LastSol.OxygenUnits.Add(plant);
                int local = i == 0 ? BaseOxygenStored : oxygenLocal[i - 1];
                if (local >= LocalStorageCapacity) plant.Reason = "Depósito local lleno: enviá el rover.";
                else if (!SpendEnergy(2)) plant.Reason = "Falta energía.";
                else
                {
                    plant.EnergyUsed = 2;
                    plant.Actual = Mathf.Min(plant.Nominal, LocalStorageCapacity - local);
                    oxygenMade += plant.Actual;
                    if (i == 0) BaseOxygenStored += plant.Actual; else oxygenLocal[i - 1] += plant.Actual;
                    plant.Reason = plant.Actual < plant.Nominal ? "Depósito de O2 lleno." : "Operativa.";
                }
                Accumulate(i == 0 ? LastSol.OxygenOne : LastSol.OxygenTwo, plant);
            }
            report += $"Plantas de O2: +{oxygenMade} en depósitos locales; el rover debe llevarlo al almacén. ";

            foreach (DepositState deposit in deposits)
            {
                if (!deposit.Extractor) continue;
                SystemResult result = deposit.LastResult = new SystemResult { Nominal = 8 + 3 * (deposit.ExtractorLevel - 1) + ActiveWorkers(CrewJob.Extractor, deposit.Id) * WorkerBonus(CrewJob.Extractor) };
                if (deposit.WaterStored >= LocalStorageCapacity) result.Reason = "Depósito local lleno: enviá el rover.";
                else if (!SpendEnergy(2)) result.Reason = "Falta energía.";
                else
                {
                    result.EnergyUsed = 2;
                    result.Actual = Mathf.Min(result.Nominal, LocalStorageCapacity - deposit.WaterStored);
                    deposit.WaterStored += result.Actual;
                    result.Reason = result.Actual < result.Nominal ? "Depósito local lleno; enviá el rover." : "Operativa.";
                }
                Accumulate(LastSol.Water, result);
            }
            if (ExtractorCount > 0) report += $"Extractores de hielo: +{LastSol.Water.Actual} agua en depósitos locales. ";

            for (int i = 0; i < greenhouseCells.Count; i++)
            {
                GreenhouseCrop crop = GreenhouseCropAt(i);
                int cropDuration = GreenhouseCropDuration(crop);
                int cropYield = GreenhouseCropYield(i);
                SystemResult result = new SystemResult { Nominal = cropYield };
                LastSol.GreenhouseUnits.Add(result);
                if (IsDamaged(DamagedStructureKind.Greenhouse, i)) result.Reason = "Invernadero averiado: reparalo.";
                else if (greenhouseLocal[i] >= LocalStorageCapacity) result.Reason = "Depósito local lleno: enviá el rover.";
                else if (Water == 0) result.Reason = "Falta agua.";
                else if (!SpendEnergy(2)) result.Reason = "Falta energía.";
                else
                {
                    result.EnergyUsed = 2;
                    result.OtherUsed = 1;
                    Water--;
                    greenhouseCropProgress[i]++;
                    if (greenhouseCropProgress[i] >= cropDuration)
                    {
                        greenhouseCropProgress[i] = 0;
                        int harvest = cropYield + ActiveWorkers(CrewJob.Greenhouse, i) * WorkerBonus(CrewJob.Greenhouse);
                        result.Actual = Mathf.Min(harvest, LocalStorageCapacity - greenhouseLocal[i]);
                        greenhouseLocal[i] += result.Actual;
                        result.Reason = result.Actual < harvest ? $"Cosecha parcial de {GreenhouseCropName(crop)}: depósito local casi lleno." : $"Cosecha de {GreenhouseCropName(crop)} lista.";
                    }
                    else result.Reason = $"Cultivando {GreenhouseCropName(crop)}: {greenhouseCropProgress[i]}/{cropDuration} soles.";
                }
                Accumulate(LastSol.Greenhouse, result);
            }
            if (Greenhouse) report += $"Invernaderos: -{LastSol.Greenhouse.OtherUsed} agua, +{LastSol.Greenhouse.Actual} comida local. ";

            foreach (DepositState deposit in deposits)
            {
                if (deposit.MineLevel == 0) continue;
                SystemResult result = deposit.LastResult = new SystemResult { Nominal = deposit.MineNominal + ActiveWorkers(CrewJob.Mine, deposit.Id) * WorkerBonus(CrewJob.Mine) };
                if (IsDamaged(DamagedStructureKind.Mine, deposit.Id)) result.Reason = "Mina averiada: reparala.";
                else if (deposit.MineStored >= deposit.MineCapacity) result.Reason = "Almacén lleno: enviá el rover.";
                else if (!SpendEnergy(deposit.MineEnergy)) result.Reason = "Falta energía.";
                else
                {
                    result.EnergyUsed = deposit.MineEnergy;
                    result.Actual = Mathf.Min(result.Nominal, deposit.MineCapacity - deposit.MineStored);
                    deposit.MineStored += result.Actual;
                    result.Reason = deposit.MineStored == deposit.MineCapacity ? "Almacén lleno: enviá el rover." : "Operativa.";
                }
                Accumulate(LastSol.Mine, result);
            }
            if (MineCount > 0) report += $"Minas ({MineCount}): +{LastSol.Mine.Actual} Fe almacenado. ";

            LastSol.CrewConsumed = Population;
            Oxygen = Mathf.Max(0, Oxygen - Population);
            Water = Mathf.Max(0, Water - Population);
            Food = Mathf.Max(0, Food - Population);
            report += $"Tripulación ({Population}): -{Population} O2, agua y comida. ";

            if (ShipmentArrivalSol != 0 && Sol >= ShipmentArrivalSol)
            {
                int foodDelivered = AddMaterial(MaterialResource.Food, ShipmentFoodRemaining);
                int waterDelivered = AddMaterial(MaterialResource.Water, ShipmentWaterRemaining);
                ShipmentFoodRemaining -= foodDelivered;
                ShipmentWaterRemaining -= waterDelivered;
                LastSol.ShipmentDelivered = foodDelivered + waterDelivered > 0;
                if (ShipmentFoodRemaining == 0 && ShipmentWaterRemaining == 0)
                {
                    ShipmentArrivalSol = 0;
                    report += $"Llegó el pedido de la Tierra: +{waterDelivered} agua y +{foodDelivered} comida. ";
                }
                else
                {
                    ShipmentArrivalSol = Sol + 1;
                    report += $"El almacén recibió +{waterDelivered} agua y +{foodDelivered} comida; el espacio restante queda pendiente para el sol {ShipmentArrivalSol}. ";
                }
            }
            if (Sol == 5)
            {
                int defended = 0;
                foreach (BarrierState barrier in barriers)
                    if (map == null || SpaceFarmMap.CellDistance(barrier.Cell, map.BaseCell) <= 7) defended++;
                int foodLoss = defended >= 8 ? 1 : defended >= 5 ? 2 : defended >= 2 ? 3 : 4;
                LastSol.RaidFoodLoss = foodLoss;
                Food = Mathf.Max(0, Food - foodLoss);
                report += $"Incursión alienígena: {defended} muros cerca del hábitat, -{foodLoss} comida; las minas siguen operativas. ";
            }
            if (Oxygen == 0 || Water == 0 || Food == 0)
            {
                Health--;
                report += $"Faltan recursos vitales: salud de la tripulación -1 (queda {Health}). ";
            }
            if (GameOver) report += "La misión fracasó. ";
            else if (MissionComplete) report += "¡Misión completada: colonia estable tras la primera incursión! ";
            return LastReport = report;
        }

        private static void Accumulate(SystemResult total, SystemResult part)
        {
            total.Nominal += part.Nominal;
            total.Actual += part.Actual;
            total.EnergyUsed += part.EnergyUsed;
            total.OtherUsed += part.OtherUsed;
            total.Reason = part.Reason;
        }

        private bool SpendEnergy(int amount)
        {
            if (Energy < amount) return false;
            Energy -= amount;
            return true;
        }

        private bool Fail(string value, out string message)
        {
            message = LastReport = value;
            return false;
        }

        private bool Success(string value, out string message)
        {
            message = LastReport = value;
            return true;
        }
    }
}
