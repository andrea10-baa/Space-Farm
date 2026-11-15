using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFarm
{
    public enum StructureKind { None, Mine, Extractor, Greenhouse, OxygenPlant, Barrier }
    public enum CrewJob { None, Mine, Extractor, Greenhouse, Oxygen, Solar }

    /// <summary>Small deterministic simulation for the colony loop. Knows deposits by id, not by map position.</summary>
    [Serializable]
    public sealed class SpaceFarmState
    {
        public const int MaxLevel = 5;
        public const float UpgradeSeconds = 30f;
        private static readonly int[] LevelXp = { 0, 0, 25, 70, 140, 240 };
        private static readonly int[] RoverRanges = { 0, 11, 24, 39, 56, 999 };
        public const int MaxMineCount = 3;
        public const int MaxExtractors = 5;
        private readonly List<BarrierState> barriers = new List<BarrierState>();
        private readonly List<Vector2Int> greenhouseCells = new List<Vector2Int>();
        private readonly List<Vector2Int> oxygenCells = new List<Vector2Int>();
        private readonly List<CrewMember> crew = new List<CrewMember>();
        private readonly List<UpgradeTask> upgrades = new List<UpgradeTask>();
        private readonly bool[] discoveries;
        private readonly int[] discoveryKinds;
        private readonly List<DepositState> deposits = new List<DepositState>();
        private readonly SpaceFarmMap map;

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
            public bool Extractor { get; internal set; }
            public SystemResult LastResult { get; internal set; } = new SystemResult();
            public int MineCapacity => MineLevel == 2 ? 36 : MineLevel == 1 ? 18 : 0;
            public int MineNominal => MineLevel == 2 ? 8 : MineLevel == 1 ? 5 : 0;
            public int MineEnergy => MineLevel == 2 ? 3 : MineLevel == 1 ? 2 : 0;
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
        }

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
        public int HabitatCapacity => ColonyLevel * 2;
        public int RoverRange => RoverRanges[ColonyLevel];
        public int SolarLevel { get; private set; } = 1;
        public int EnergyCapacity => 30 + (SolarLevel - 1) * 10;
        public int SolarPerSol => 14 + (SolarLevel - 1) * 12;
        public int Health { get; private set; } = 3;
        public int OxygenPlants { get; private set; } = 1;
        public int RoverLevel { get; private set; } = 1;
        public bool Greenhouse { get; private set; }
        public Vector2Int? GreenhouseCell { get; private set; }
        public Vector2Int? OxygenPlantTwoCell { get; private set; }
        public int BarrierCount => barriers.Count;
        public int RoverCargo { get; private set; }
        public int RoverCapacity => RoverLevel == 2 ? 24 : 12;
        public int ShipmentArrivalSol { get; private set; }
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
        public int ExtractorCount => CountWhere(d => d.Extractor);
        public bool AnySurveyed(DepositKind kind) => CountWhere(d => d.Kind == kind && d.Surveyed) > 0;
        /// <summary>Every mine needs an operator: two colonists run one mine, each extra colonist one more.</summary>
        public int MaxMines => Mathf.Clamp(Population - 1, 1, MaxMineCount);
        public int MineCost => 8 + 4 * MineCount;
        public int ExtractorCost => 6 + 4 * ExtractorCount;

        /// <summary>Minimal layout for rule checks: one iron deposit (id 0), one ice deposit (id 1) and three discoveries.</summary>
        public SpaceFarmState() : this(DefaultDeposits(), new[] { 0, 1, 2 }) { }

        public SpaceFarmState(SpaceFarmMap map) : this(map.Deposits, PointKinds(map)) { this.map = map; }

        public SpaceFarmState(IReadOnlyList<MapDeposit> layout, IReadOnlyList<int> pointKinds)
        {
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

        public int StructureCost(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Mine: return MineCost;
                case StructureKind.Extractor: return ExtractorCost;
                case StructureKind.Greenhouse: return 6;
                case StructureKind.OxygenPlant: return 9;
                case StructureKind.Barrier: return 1;
                default: return 0;
            }
        }

        /// <summary>Catalog availability, independent of the pointer position.</summary>
        public string PlacementAvailability(StructureKind kind)
        {
            if (kind == StructureKind.Greenhouse && greenhouseCells.Count >= 5) return "Límite de 5 invernaderos";
            if (kind == StructureKind.OxygenPlant && OxygenPlants >= 5) return "Límite de 5 plantas de O2";
            if (kind == StructureKind.Mine)
            {
                if (MineCount >= MaxMineCount) return "Límite de 3 minas";
                if (MineCount >= MaxMines) return $"Falta un operador: {Population} colonos sostienen {MaxMines} mina(s)";
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
            if (!revealed) return "Sector oculto: exploralo con el rover";
            string availability = PlacementAvailability(kind);
            if (availability.Length > 0) return availability;
            if (HasBarrierInFootprint(cell, 1, 1)) return "No se puede colocar una estructura sobre un muro";
            if (IsCellOccupiedByStructure(cell)) return "Ese sector está ocupado por una estructura";
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

        private static bool Near(Vector2Int a, Vector2Int b, int radius) =>
            Mathf.Abs(a.x - b.x) <= radius && Mathf.Abs(a.y - b.y) <= radius;

        public bool IsCellOccupiedByStructure(Vector2Int cell)
        {
            if (map == null) return false;
            Vector2Int baseCell = map.BaseCell;
            if (Within(cell, baseCell, 2, 1) ||
                Within(cell, baseCell + new Vector2Int(2, 0), 1, 1) ||
                Within(cell, baseCell + new Vector2Int(-2, -1), 1, 1) ||
                Within(cell, baseCell + new Vector2Int(-1, 2), 1, 1)) return true;
            foreach (Vector2Int placed in greenhouseCells) if (Within(cell, placed, 1, 1)) return true;
            foreach (Vector2Int placed in oxygenCells) if (Within(cell, placed, 1, 1)) return true;
            foreach (DepositState deposit in deposits)
                if ((deposit.MineLevel > 0 || deposit.Extractor) && Within(cell, map.Deposits[deposit.Id].Cell, 1, 1)) return true;
            return false;
        }

        private static bool Within(Vector2Int cell, Vector2Int center, int radiusX, int radiusY) =>
            Mathf.Abs(cell.x - center.x) <= radiusX && Mathf.Abs(cell.y - center.y) <= radiusY;

        public bool HasBarrierInFootprint(Vector2Int cell, int radiusX, int radiusY)
        {
            for (int i = 0; i < barriers.Count; i++)
                if (Within(cell, barriers[i].Cell, radiusX, radiusY)) return true;
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
                if (!GreenhouseCell.HasValue) GreenhouseCell = cell;
                GainXp(6);
                return Success("Invernadero listo: transforma agua y energía en 7 de comida por sol.", out message);
            }
            OxygenPlants++;
            oxygenCells.Add(cell);
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
                Iron += sample;
                return LastReport = $"Rover: yacimiento {deposit.Name} confirmado. Trajo {sample} Fe de muestra. Seleccionalo para construir una mina.";
            }
            Water = Mathf.Min(60, Water + sample);
            return LastReport = $"Rover: depósito {deposit.Name} confirmado. Trajo {sample} de agua. Seleccionalo para construir un extractor.";
        }

        /// <summary>Why a mine cannot be built there right now, or an empty string.</summary>
        public string MineBlocker(int id)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null || deposit.Kind != DepositKind.Iron) return "Elegí un yacimiento de hierro";
            if (!deposit.Surveyed) return "Primero estudiá ese yacimiento con el rover";
            if (deposit.MineLevel > 0) return "Ese yacimiento ya tiene mina";
            if (MineCount >= MaxMineCount) return $"La demo admite {MaxMineCount} minas";
            if (MineCount >= MaxMines) return $"Cada mina necesita un operador: con {Population} colonos operás {MaxMines}";
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
            if (deposit.MineLevel >= 2) return Fail("El nivel 2 es el máximo de esta demo.", out message);
            if (HasUpgrade("mine", id)) return Fail("Esa mina ya se está mejorando.", out message);
            if (Iron < 6) return Fail("La mejora necesita 6 de hierro.", out message);
            Iron -= 6;
            upgrades.Add(new UpgradeTask { Kind = "mine", Target = id, Remaining = UpgradeSeconds });
            return Success($"Mejora de {deposit.Name} iniciada: 30 segundos.", out message);
        }

        public bool UpgradeRover(out string message)
        {
            if (RoverLevel >= 2) return Fail("El nivel 2 es el máximo de esta demo.", out message);
            if (HasUpgrade("rover", 0)) return Fail("El rover ya se está mejorando.", out message);
            if (Iron < 5) return Fail("El rover necesita 5 de hierro para la mejora.", out message);
            Iron -= 5;
            upgrades.Add(new UpgradeTask { Kind = "rover", Remaining = UpgradeSeconds });
            return Success("Mejora del rover iniciada: 30 segundos; tendrá más capacidad y visión.", out message);
        }

        public bool UpgradeSolar(out string message)
        {
            if (SolarLevel >= MaxLevel) return Fail("Los paneles están en nivel máximo.", out message);
            if (HasUpgrade("solar", 0)) return Fail("Los paneles ya se están mejorando.", out message);
            int cost = SolarUpgradeCost;
            if (Iron < cost) return Fail($"La mejora solar necesita {cost} Fe.", out message);
            Iron -= cost;
            upgrades.Add(new UpgradeTask { Kind = "solar", Remaining = UpgradeSeconds });
            return Success($"Ampliación solar iniciada: 30 segundos. Costo: {cost} Fe.", out message);
        }

        public int SolarUpgradeCost => 5 + SolarLevel * 3;
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
                if (task.Kind == "mine") Deposit(task.Target).MineLevel = 2;
                else if (task.Kind == "rover") RoverLevel = 2;
                else if (task.Kind == "solar") SolarLevel++;
                upgrades.RemoveAt(i);
                GainXp(6);
                LastReport = $"Mejora de {task.Kind} completada.";
                changed = true;
            }
            foreach (CrewMember member in crew)
            {
                if (member.Resting)
                {
                    member.Energy = Mathf.Min(100f, member.Energy + seconds * 5f);
                    if (member.Energy >= 95f) { member.Resting = false; changed = true; }
                }
                else if (member.Job != CrewJob.None)
                {
                    member.Energy = Mathf.Max(0f, member.Energy - seconds * 1.8f);
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
            if (RoverCargo > 0) return Fail("Descargá primero la carga del rover.", out message);
            if (deposit.MineStored == 0) return Fail("La mina no tiene hierro para recoger.", out message);
            int loaded = Mathf.Min(deposit.MineStored, RoverCapacity);
            deposit.MineStored -= loaded;
            RoverCargo = loaded;
            return Success($"Rover cargó {loaded}/{RoverCapacity} Fe en {deposit.Name}. Regresá al hábitat para descargar.", out message);
        }

        public bool UnloadRover(out string message)
        {
            if (RoverCargo == 0) return Fail("El rover no lleva carga.", out message);
            int amount = RoverCargo;
            Iron += amount;
            RoverCargo = 0;
            GainXp(2);
            return Success($"Rover descargó +{amount} Fe en el hábitat.", out message);
        }

        public string ClaimDiscovery(int index)
        {
            if (index < 0 || index >= discoveries.Length || discoveries[index])
                return "Ese punto ya fue explorado.";
            discoveries[index] = true;
            GainXp(12);
            switch (discoveryKinds[index])
            {
                case 0: Iron += 2; return LastReport = "Cápsula científica: +2 Fe recuperado.";
                case 1: Water = Mathf.Min(60, Water + 3); return LastReport = "Hielo superficial: +3 agua recuperada.";
                case 2: Energy = Mathf.Min(EnergyCapacity, Energy + 4); return LastReport = "Batería abandonada: +4 energía recuperada.";
                default: Food = Mathf.Min(60, Food + 3); return LastReport = "Contenedor de víveres: +3 comida recuperada.";
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
            return Success($"Pedido enviado: llegará en el sol {ShipmentArrivalSol} con comida y agua.", out message);
        }

        public string AdvanceSol()
        {
            if (GameOver) return LastReport = "La misión terminó. Abrí el menú de pausa para reiniciar.";
            Sol++;
            GainXp(4);
            bool storm = Sol == 3 || Sol == 4;
            int solarNominal = SolarPerSol + ActiveWorkers(CrewJob.Solar, 0) * WorkerBonus(CrewJob.Solar);
            int solar = storm ? Mathf.Max(3, solarNominal / 3) : solarNominal;
            LastSol = new SolResults { Storm = storm };
            LastSol.Solar.Nominal = solarNominal;
            LastSol.Solar.Actual = Mathf.Min(solar, EnergyCapacity - Energy);
            LastSol.Solar.Reason = storm ? "Tormenta de polvo: llega menos luz." :
                LastSol.Solar.Actual < solar ? "Batería llena." : "Operativa.";
            Energy += LastSol.Solar.Actual;
            string report = $"Sol {Sol}: los paneles generaron {LastSol.Solar.Actual} de energía";
            if (storm) report += " por la tormenta de polvo";
            report += ". ";

            int oxygenMade = 0;
            for (int i = 0; i < OxygenPlants; i++)
            {
                SystemResult plant = new SystemResult { Nominal = 7 + ActiveWorkers(CrewJob.Oxygen, i) * WorkerBonus(CrewJob.Oxygen) };
                LastSol.OxygenUnits.Add(plant);
                if (Oxygen + oxygenMade >= 60) plant.Reason = "Depósito de O2 lleno.";
                else if (!SpendEnergy(2)) plant.Reason = "Falta energía.";
                else
                {
                    plant.EnergyUsed = 2;
                    plant.Actual = Mathf.Min(plant.Nominal, 60 - Oxygen - oxygenMade);
                    oxygenMade += plant.Actual;
                    plant.Reason = plant.Actual < plant.Nominal ? "Depósito de O2 lleno." : "Operativa.";
                }
                Accumulate(i == 0 ? LastSol.OxygenOne : LastSol.OxygenTwo, plant);
            }
            Oxygen += oxygenMade;
            report += $"Plantas de O2: +{oxygenMade}. ";

            foreach (DepositState deposit in deposits)
            {
                if (!deposit.Extractor) continue;
                SystemResult result = deposit.LastResult = new SystemResult { Nominal = 8 + ActiveWorkers(CrewJob.Extractor, deposit.Id) * WorkerBonus(CrewJob.Extractor) };
                if (Water >= 60) result.Reason = "Depósito de agua lleno.";
                else if (!SpendEnergy(2)) result.Reason = "Falta energía.";
                else
                {
                    result.EnergyUsed = 2;
                    result.Actual = Mathf.Min(result.Nominal, 60 - Water);
                    Water += result.Actual;
                    result.Reason = result.Actual < result.Nominal ? "Depósito de agua lleno." : "Operativa.";
                }
                Accumulate(LastSol.Water, result);
            }
            if (ExtractorCount > 0) report += $"Extractores de hielo: +{LastSol.Water.Actual} agua. ";

            for (int i = 0; i < greenhouseCells.Count; i++)
            {
                SystemResult result = new SystemResult { Nominal = 7 + ActiveWorkers(CrewJob.Greenhouse, i) * WorkerBonus(CrewJob.Greenhouse) };
                LastSol.GreenhouseUnits.Add(result);
                if (Food >= 60) result.Reason = "Almacén de comida lleno.";
                else if (Water == 0) result.Reason = "Falta agua.";
                else if (!SpendEnergy(2)) result.Reason = "Falta energía.";
                else
                {
                    result.EnergyUsed = 2;
                    result.OtherUsed = 1;
                    Water--;
                    result.Actual = Mathf.Min(result.Nominal, 60 - Food);
                    Food += result.Actual;
                    result.Reason = result.Actual < result.Nominal ? "Almacén de comida lleno." : "Operativa.";
                }
                Accumulate(LastSol.Greenhouse, result);
            }
            if (Greenhouse) report += $"Invernaderos: -{LastSol.Greenhouse.OtherUsed} agua, +{LastSol.Greenhouse.Actual} comida. ";

            foreach (DepositState deposit in deposits)
            {
                if (deposit.MineLevel == 0) continue;
                SystemResult result = deposit.LastResult = new SystemResult { Nominal = deposit.MineNominal + ActiveWorkers(CrewJob.Mine, deposit.Id) * WorkerBonus(CrewJob.Mine) };
                if (deposit.MineStored >= deposit.MineCapacity) result.Reason = "Almacén lleno: enviá el rover.";
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
                ShipmentArrivalSol = 0;
                Food = Mathf.Min(60, Food + 5);
                Water = Mathf.Min(60, Water + 5);
                LastSol.ShipmentDelivered = true;
                report += "Llegó el pedido de la Tierra: +5 agua y +5 comida. ";
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
