using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFarm
{
    /// <summary>Small deterministic simulation for the colony loop. Knows deposits by id, not by map position.</summary>
    [Serializable]
    public sealed class SpaceFarmState
    {
        public const int BarrierSlotCount = 12;
        public const int MaxMineCount = 3;
        public const int MaxExtractors = 2;
        private readonly bool[] barrierSegments = new bool[BarrierSlotCount];
        private readonly bool[] discoveries;
        private readonly int[] discoveryKinds;
        private readonly List<DepositState> deposits = new List<DepositState>();

        public sealed class SystemResult
        {
            public int Nominal;
            public int Actual;
            public int EnergyUsed;
            public int OtherUsed;
            public string Reason = "Aún no se avanzó un sol.";
        }

        /// <summary>One iron or ice deposit with the installation built on it. Each keeps its own level, damage and storage.</summary>
        public sealed class DepositState
        {
            public int Id { get; internal set; }
            public DepositKind Kind { get; internal set; }
            public int Distance { get; internal set; }
            public string Name { get; internal set; }
            public bool Surveyed { get; internal set; }
            public int MineLevel { get; internal set; }
            public int MineStored { get; internal set; }
            public bool MineDamaged { get; internal set; }
            public bool Extractor { get; internal set; }
            public SystemResult LastResult { get; internal set; } = new SystemResult();
            public int MineCapacity => MineLevel == 2 ? 15 : MineLevel == 1 ? 6 : 0;
            public int MineNominal => MineLevel == 2 ? 5 : MineLevel == 1 ? 3 : 0;
            public int MineEnergy => MineLevel == 2 ? 3 : MineLevel == 1 ? 2 : 0;
            public bool HasBuilding => MineLevel > 0 || Extractor;
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
            public bool Storm;
            public int CrewConsumed;
            public int RaidFoodLoss;
            public int RaidDamagedMines;
            public bool RaidDamagedMine => RaidDamagedMines > 0;
        }

        public SolResults LastSol { get; private set; } = new SolResults();
        public int Sol { get; private set; } = 1;
        public int Iron { get; private set; } = 18;
        public int Energy { get; private set; } = 12;
        public int Oxygen { get; private set; } = 12;
        public int Water { get; private set; } = 14;
        public int Food { get; private set; } = 14;
        public int Population { get; private set; } = 2;
        public int Health { get; private set; } = 3;
        public int OxygenPlants { get; private set; } = 1;
        public int RoverLevel { get; private set; } = 1;
        public bool Greenhouse { get; private set; }
        public int BarrierCount { get; private set; }
        public int RoverCargo { get; private set; }
        public int RoverCapacity => RoverLevel == 2 ? 8 : 4;
        public int ShipmentArrivalSol { get; private set; }
        public bool GameOver => Health <= 0;
        public bool MissionComplete => Sol >= 7 && MineCount > 0 && Greenhouse && Population >= 3 && BarrierCount >= 8 && !GameOver;
        public int ScanRadius => RoverLevel == 1 ? 2 : 3;
        public int OxygenPerSol => OxygenPlants * 4;
        public string LastReport { get; private set; } = "La nave aterrizó. Enviá el rover a explorar los sectores ocultos.";

        public IReadOnlyList<DepositState> Deposits => deposits;
        public int DiscoveryCount => discoveries.Length;
        public bool HasBarrierSegment(int index) => index >= 0 && index < BarrierSlotCount && barrierSegments[index];
        public bool HasDiscovery(int index) => index >= 0 && index < discoveries.Length && discoveries[index];
        public DepositState Deposit(int id) => id >= 0 && id < deposits.Count ? deposits[id] : null;

        public int MineCount => CountWhere(d => d.MineLevel > 0);
        public int ExtractorCount => CountWhere(d => d.Extractor);
        public bool AnyMineDamaged => CountWhere(d => d.MineDamaged) > 0;
        public bool AnySurveyed(DepositKind kind) => CountWhere(d => d.Kind == kind && d.Surveyed) > 0;
        /// <summary>Every mine needs an operator: two colonists run one mine, each extra colonist one more.</summary>
        public int MaxMines => Mathf.Clamp(Population - 1, 1, MaxMineCount);
        public int MineCost => 8 + 4 * MineCount;
        public int ExtractorCost => 6 + 4 * ExtractorCount;

        /// <summary>Minimal layout for rule checks: one iron deposit (id 0), one ice deposit (id 1) and three discoveries.</summary>
        public SpaceFarmState() : this(DefaultDeposits(), new[] { 0, 1, 2 }) { }

        public SpaceFarmState(SpaceFarmMap map) : this(map.Deposits, PointKinds(map)) { }

        public SpaceFarmState(IReadOnlyList<MapDeposit> layout, IReadOnlyList<int> pointKinds)
        {
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

        public string SurveyDeposit(int id)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null) return "Ahí no hay ningún yacimiento.";
            if (deposit.Surveyed) return $"{deposit.Name} ya está estudiado.";
            // The first sample of each resource is the big one; later surveys bring a single unit.
            int sample = AnySurveyed(deposit.Kind) ? 1 : 2;
            deposit.Surveyed = true;
            if (deposit.Kind == DepositKind.Iron)
            {
                Iron += sample;
                return LastReport = $"Rover: yacimiento {deposit.Name} confirmado. Trajo {sample} Fe de muestra. Seleccionalo para construir una mina.";
            }
            Water = Mathf.Min(30, Water + sample);
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
            return Success($"Mina nivel 1 construida en {deposit.Name}. Produce hasta 3 Fe por sol, usa 2 de energía y almacena 6. Enviá el rover para recoger.", out message);
        }

        public bool UpgradeMine(int id, out string message)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null || deposit.MineLevel == 0) return Fail("Construí primero la mina.", out message);
            if (deposit.MineLevel >= 2) return Fail("El nivel 2 es el máximo de esta demo.", out message);
            if (deposit.MineDamaged) return Fail("Repará la mina antes de mejorarla.", out message);
            if (Iron < 6) return Fail("La mejora necesita 6 de hierro.", out message);
            Iron -= 6;
            deposit.MineLevel = 2;
            return Success($"Mina de {deposit.Name} en nivel 2. Produce hasta 5 Fe por sol, almacena 15 y requiere 3 de energía.", out message);
        }

        public bool RepairMine(int id, out string message)
        {
            DepositState deposit = Deposit(id);
            if (deposit == null || !deposit.MineDamaged) return Fail("Esa mina no necesita reparación.", out message);
            if (Iron < 3) return Fail("La reparación necesita 3 de hierro.", out message);
            Iron -= 3;
            deposit.MineDamaged = false;
            return Success($"Mina de {deposit.Name} reparada y operativa.", out message);
        }

        public bool UpgradeRover(out string message)
        {
            if (RoverLevel >= 2) return Fail("El nivel 2 es el máximo de esta demo.", out message);
            if (Iron < 5) return Fail("El rover necesita 5 de hierro para la mejora.", out message);
            Iron -= 5;
            RoverLevel = 2;
            return Success("Rover nivel 2: revela sectores a 3 casillas y carga 8 Fe por viaje.", out message);
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
            return Success($"Extractor listo en {deposit.Name}: obtiene 5 de agua por sol a cambio de 2 de energía.", out message);
        }

        public bool BuildGreenhouse(out string message)
        {
            if (Greenhouse) return Fail("El invernadero ya está construido.", out message);
            if (Iron < 6) return Fail("El invernadero necesita 6 de hierro.", out message);
            Iron -= 6;
            Greenhouse = true;
            return Success("Invernadero listo: transforma agua y energía en 4 de comida por sol.", out message);
        }

        public bool BuildOxygenPlant(out string message)
        {
            if (OxygenPlants >= 2) return Fail("La demo admite como máximo dos plantas de oxígeno.", out message);
            if (Iron < 9) return Fail("Otra planta de oxígeno necesita 9 de hierro.", out message);
            Iron -= 9;
            OxygenPlants++;
            return Success("Nueva planta de oxígeno lista. Cada planta genera 4 de O2 y consume 3 de energía por sol.", out message);
        }

        public bool BuildBarrierSegment(int index, out string message)
        {
            if (index < 0 || index >= BarrierSlotCount) return Fail("Esa posición no admite barrera.", out message);
            if (barrierSegments[index]) return Fail("Ese segmento ya está construido.", out message);
            if (Iron < 1) return Fail("Cada segmento cuesta 1 Fe.", out message);
            Iron--;
            barrierSegments[index] = true;
            BarrierCount++;
            return Success($"Segmento instalado. Perímetro {BarrierCount}/{BarrierSlotCount}; desde 8 las minas quedan protegidas.", out message);
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
            return Success($"Rover descargó +{amount} Fe en el hábitat.", out message);
        }

        public string ClaimDiscovery(int index)
        {
            if (index < 0 || index >= discoveries.Length || discoveries[index])
                return "Ese punto ya fue explorado.";
            discoveries[index] = true;
            switch (discoveryKinds[index])
            {
                case 0: Iron += 2; return LastReport = "Cápsula científica: +2 Fe recuperado.";
                case 1: Water = Mathf.Min(30, Water + 3); return LastReport = "Hielo superficial: +3 agua recuperada.";
                case 2: Energy = Mathf.Min(30, Energy + 4); return LastReport = "Batería abandonada: +4 energía recuperada.";
                default: Food = Mathf.Min(30, Food + 3); return LastReport = "Contenedor de víveres: +3 comida recuperada.";
            }
        }

        public bool AddColonist(out string message)
        {
            if (Population >= 4) return Fail("El hábitat de la demo admite como máximo 4 personas.", out message);
            if (OxygenPerSol < (Population + 1) * 1.25f)
                return Fail("Falta producción de O2 con un 25 % de reserva para otro colono.", out message);
            if (Oxygen < 8 || Water < 8 || Food < 8)
                return Fail("Necesitás al menos 8 de O2, agua y comida antes de recibir otra persona.", out message);
            Population++;
            return Success($"Llegó un nuevo colono. Aumentó el consumo diario y ya podés operar {MaxMines} mina(s).", out message);
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
            bool storm = Sol == 3 || Sol == 4;
            int solar = storm ? 2 : 9;
            LastSol = new SolResults { Storm = storm };
            LastSol.Solar.Nominal = 9;
            LastSol.Solar.Actual = Mathf.Min(solar, 30 - Energy);
            LastSol.Solar.Reason = storm ? "Tormenta de polvo: llega menos luz." :
                LastSol.Solar.Actual < solar ? "Batería llena." : "Operativa.";
            Energy += LastSol.Solar.Actual;
            string report = $"Sol {Sol}: los paneles generaron {LastSol.Solar.Actual} de energía";
            if (storm) report += " por la tormenta de polvo";
            report += ". ";

            int oxygenMade = 0;
            for (int i = 0; i < OxygenPlants; i++)
            {
                SystemResult plant = i == 0 ? LastSol.OxygenOne : LastSol.OxygenTwo;
                plant.Nominal = 4;
                if (Oxygen + oxygenMade >= 30) plant.Reason = "Depósito de O2 lleno.";
                else if (!SpendEnergy(3)) plant.Reason = "Falta energía.";
                else
                {
                    plant.EnergyUsed = 3;
                    plant.Actual = Mathf.Min(4, 30 - Oxygen - oxygenMade);
                    oxygenMade += plant.Actual;
                    plant.Reason = plant.Actual < 4 ? "Depósito de O2 lleno." : "Operativa.";
                }
            }
            Oxygen += oxygenMade;
            report += $"Plantas de O2: +{oxygenMade}. ";

            foreach (DepositState deposit in deposits)
            {
                if (!deposit.Extractor) continue;
                SystemResult result = deposit.LastResult = new SystemResult { Nominal = 5 };
                if (Water >= 30) result.Reason = "Depósito de agua lleno.";
                else if (!SpendEnergy(2)) result.Reason = "Falta energía.";
                else
                {
                    result.EnergyUsed = 2;
                    result.Actual = Mathf.Min(5, 30 - Water);
                    Water += result.Actual;
                    result.Reason = result.Actual < 5 ? "Depósito de agua lleno." : "Operativa.";
                }
                Accumulate(LastSol.Water, result);
            }
            if (ExtractorCount > 0) report += $"Extractores de hielo: +{LastSol.Water.Actual} agua. ";

            if (Greenhouse)
            {
                LastSol.Greenhouse.Nominal = 4;
                if (Food >= 30) LastSol.Greenhouse.Reason = "Almacén de comida lleno.";
                else if (Water == 0) LastSol.Greenhouse.Reason = "Falta agua.";
                else if (!SpendEnergy(2)) LastSol.Greenhouse.Reason = "Falta energía.";
                else
                {
                    LastSol.Greenhouse.EnergyUsed = 2;
                    LastSol.Greenhouse.OtherUsed = 1;
                    Water--;
                    LastSol.Greenhouse.Actual = Mathf.Min(4, 30 - Food);
                    Food += LastSol.Greenhouse.Actual;
                    LastSol.Greenhouse.Reason = LastSol.Greenhouse.Actual < 4 ? "Almacén de comida lleno." : "Operativa.";
                    report += $"Invernadero: -1 agua, +{LastSol.Greenhouse.Actual} comida. ";
                }
            }

            foreach (DepositState deposit in deposits)
            {
                if (deposit.MineLevel == 0) continue;
                SystemResult result = deposit.LastResult = new SystemResult { Nominal = deposit.MineNominal };
                if (deposit.MineDamaged) result.Reason = "Averiada: repará la mina.";
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
                ShipmentArrivalSol = 0;
                Food = Mathf.Min(30, Food + 5);
                Water = Mathf.Min(30, Water + 5);
                report += "Llegó el pedido de la Tierra: +5 agua y +5 comida. ";
            }
            if (Sol == 5)
            {
                int foodLoss = BarrierCount >= 12 ? 1 : BarrierCount >= 8 ? 2 : BarrierCount >= 4 ? 3 : 4;
                LastSol.RaidFoodLoss = foodLoss;
                Food = Mathf.Max(0, Food - foodLoss);
                LastSol.RaidDamagedMines = RaidMines();
                report += $"Incursión alienígena: perímetro {BarrierCount}/12, -{foodLoss} comida";
                report += LastSol.RaidDamagedMines > 0 ? $"; {LastSol.RaidDamagedMines} mina(s) averiada(s). " : "; minas protegidas. ";
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

        /// <summary>
        /// Raid exposure grows with the territory: under 4 segments every mine is hit,
        /// with 4 to 7 only the mine farthest from the base, and from 8 all of them are safe.
        /// </summary>
        private int RaidMines()
        {
            if (BarrierCount >= 8) return 0;
            int damaged = 0;
            DepositState farthest = null;
            foreach (DepositState deposit in deposits)
            {
                if (deposit.MineLevel == 0) continue;
                if (farthest == null || deposit.Distance > farthest.Distance) farthest = deposit;
                if (BarrierCount < 4)
                {
                    deposit.MineDamaged = true;
                    damaged++;
                }
            }
            if (BarrierCount >= 4 && farthest != null)
            {
                farthest.MineDamaged = true;
                damaged = 1;
            }
            return damaged;
        }

        private static void Accumulate(SystemResult total, SystemResult part)
        {
            total.Nominal += part.Nominal;
            total.Actual += part.Actual;
            total.EnergyUsed += part.EnergyUsed;
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
