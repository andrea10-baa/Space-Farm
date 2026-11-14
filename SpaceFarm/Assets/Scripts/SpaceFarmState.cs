using System;
using UnityEngine;

namespace SpaceFarm
{
    /// <summary>Small deterministic simulation for the first playable colony loop.</summary>
    [Serializable]
    public sealed class SpaceFarmState
    {
        public const int BarrierSlotCount = 12;
        private readonly bool[] barrierSegments = new bool[BarrierSlotCount];
        private readonly bool[] discoveries = new bool[3];
        public sealed class SystemResult
        {
            public int Nominal;
            public int Actual;
            public int EnergyUsed;
            public int OtherUsed;
            public string Reason = "Aún no se avanzó un sol.";
        }
        public sealed class SolResults
        {
            public readonly SystemResult Solar = new SystemResult();
            public readonly SystemResult OxygenOne = new SystemResult();
            public readonly SystemResult OxygenTwo = new SystemResult();
            public readonly SystemResult Water = new SystemResult();
            public readonly SystemResult Greenhouse = new SystemResult();
            public readonly SystemResult Mine = new SystemResult();
            public bool Storm;
            public int CrewConsumed;
            public int RaidFoodLoss;
            public bool RaidDamagedMine;
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
        public int MineLevel { get; private set; }
        public int RoverLevel { get; private set; } = 1;
        public bool IronSurveyed { get; private set; }
        public bool IceSurveyed { get; private set; }
        public bool Greenhouse { get; private set; }
        public bool WaterExtractor { get; private set; }
        public int BarrierCount { get; private set; }
        public bool HasBarrierSegment(int index) => index >= 0 && index < BarrierSlotCount && barrierSegments[index];
        public bool HasDiscovery(int index) => index >= 0 && index < discoveries.Length && discoveries[index];
        public int MineStored { get; private set; }
        public int MineCapacity => MineLevel == 2 ? 15 : MineLevel == 1 ? 6 : 0;
        public int RoverCargo { get; private set; }
        public int RoverCapacity => RoverLevel == 2 ? 8 : 4;
        public bool MineDamaged { get; private set; }
        public int ShipmentArrivalSol { get; private set; }
        public bool GameOver => Health <= 0;
        public bool MissionComplete => Sol >= 7 && MineLevel > 0 && Greenhouse && Population >= 3 && BarrierCount >= 8 && !GameOver;
        public int ScanRadius => RoverLevel == 1 ? 2 : 3;
        public int OxygenPerSol => OxygenPlants * 4;
        public string LastReport { get; private set; } = "La nave aterrizó. Enviá el rover a explorar los sectores ocultos.";

        public string SurveyIron()
        {
            if (IronSurveyed) return "Yacimiento de hierro ya estudiado.";
            IronSurveyed = true;
            Iron += 2;
            return LastReport = "Rover: yacimiento de hierro confirmado. Trajo 2 unidades de muestra. Ya podés construir una mina.";
        }

        public string SurveyIce()
        {
            if (IceSurveyed) return "Depósito de hielo ya estudiado.";
            IceSurveyed = true;
            Water += 2;
            return LastReport = "Rover: depósito de hielo confirmado. Trajo 2 unidades de agua. Podés construir un extractor.";
        }

        public bool BuildMine(out string message)
        {
            if (!IronSurveyed) return Fail("Primero explorá y estudiá el yacimiento de hierro.", out message);
            if (MineLevel > 0) return Fail("La mina ya está construida.", out message);
            if (Iron < 8) return Fail("La mina necesita 8 de hierro.", out message);
            Iron -= 8;
            MineLevel = 1;
            return Success("Mina nivel 1 construida. Produce hasta 3 Fe por sol y almacena 6. Enviá el rover para recoger.", out message);
        }

        public bool UpgradeMine(out string message)
        {
            if (MineLevel == 0) return Fail("Construí primero la mina.", out message);
            if (MineLevel >= 2) return Fail("El nivel 2 es el máximo de esta demo.", out message);
            if (MineDamaged) return Fail("Repará la mina antes de mejorarla.", out message);
            if (Iron < 6) return Fail("La mejora necesita 6 de hierro.", out message);
            Iron -= 6;
            MineLevel = 2;
            return Success("Mina nivel 2 lista. Produce hasta 5 Fe por sol, almacena 15 y requiere 3 de energía.", out message);
        }

        public bool RepairMine(out string message)
        {
            if (!MineDamaged) return Fail("La mina no necesita reparación.", out message);
            if (Iron < 3) return Fail("La reparación necesita 3 de hierro.", out message);
            Iron -= 3;
            MineDamaged = false;
            return Success("Mina reparada y operativa.", out message);
        }

        public bool UpgradeRover(out string message)
        {
            if (RoverLevel >= 2) return Fail("El nivel 2 es el máximo de esta demo.", out message);
            if (Iron < 5) return Fail("El rover necesita 5 de hierro para la mejora.", out message);
            Iron -= 5;
            RoverLevel = 2;
            return Success("Rover nivel 2: ahora revela sectores a 3 casillas de distancia.", out message);
        }

        public bool BuildWaterExtractor(out string message)
        {
            if (!IceSurveyed) return Fail("Primero descubrí el depósito de hielo.", out message);
            if (WaterExtractor) return Fail("El extractor de agua ya está construido.", out message);
            if (Iron < 6) return Fail("El extractor necesita 6 de hierro.", out message);
            Iron -= 6;
            WaterExtractor = true;
            return Success("Extractor listo: obtiene 5 de agua por sol a cambio de 2 de energía.", out message);
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
            return Success($"Segmento instalado. Perímetro {BarrierCount}/{BarrierSlotCount}; desde 8 la mina queda protegida.", out message);
        }

        public bool LoadMineToRover(out string message)
        {
            if (RoverCargo > 0) return Fail("Descargá primero la carga del rover.", out message);
            if (MineStored == 0) return Fail("La mina no tiene hierro para recoger.", out message);
            int loaded = Mathf.Min(MineStored, RoverCapacity);
            MineStored -= loaded;
            RoverCargo = loaded;
            return Success($"Rover cargó {loaded}/{RoverCapacity} Fe. Regresá al hábitat para descargar.", out message);
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
            if (index == 0) { Iron += 2; return LastReport = "Cápsula científica: +2 Fe recuperado."; }
            if (index == 1) { Water += 3; return LastReport = "Hielo superficial: +3 agua recuperada."; }
            Energy = Mathf.Min(30, Energy + 4);
            return LastReport = "Batería abandonada: +4 energía recuperada.";
        }

        public bool AddColonist(out string message)
        {
            if (Population >= 4) return Fail("El hábitat de la demo admite como máximo 4 personas.", out message);
            if (OxygenPerSol < (Population + 1) * 1.25f)
                return Fail("Falta producción de O2 con un 25 % de reserva para otro colono.", out message);
            if (Oxygen < 8 || Water < 8 || Food < 8)
                return Fail("Necesitás al menos 8 de O2, agua y comida antes de recibir otra persona.", out message);
            Population++;
            return Success("Llegó un nuevo colono. Aumentó la mano de obra y también el consumo diario.", out message);
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
            if (GameOver) return LastReport = "La misión terminó. Reiniciá la escena para volver a intentarlo.";
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

            if (WaterExtractor)
            {
                LastSol.Water.Nominal = 5;
                if (Water >= 30) LastSol.Water.Reason = "Depósito de agua lleno.";
                else if (!SpendEnergy(2)) LastSol.Water.Reason = "Falta energía.";
                else
                {
                    LastSol.Water.EnergyUsed = 2;
                    LastSol.Water.Actual = Mathf.Min(5, 30 - Water);
                    Water += LastSol.Water.Actual;
                    LastSol.Water.Reason = LastSol.Water.Actual < 5 ? "Depósito de agua lleno." : "Operativa.";
                    report += $"Extractor de hielo: +{LastSol.Water.Actual} agua. ";
                }
            }
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
            if (MineLevel > 0)
            {
                LastSol.Mine.Nominal = MineLevel == 1 ? 3 : 5;
                if (MineDamaged) LastSol.Mine.Reason = "Averiada: repará la mina.";
                else if (MineStored >= MineCapacity) LastSol.Mine.Reason = "Almacén lleno: enviá el rover.";
                else
                {
                    int energyCost = MineLevel == 1 ? 2 : 3;
                    if (!SpendEnergy(energyCost)) LastSol.Mine.Reason = "Falta energía.";
                    else
                    {
                        LastSol.Mine.EnergyUsed = energyCost;
                        LastSol.Mine.Actual = Mathf.Min(LastSol.Mine.Nominal, MineCapacity - MineStored);
                        MineStored += LastSol.Mine.Actual;
                        LastSol.Mine.Reason = MineStored == MineCapacity ? "Almacén lleno: enviá el rover." : "Operativa.";
                        report += $"Mina: +{LastSol.Mine.Actual} Fe almacenado ({MineStored}/{MineCapacity}). ";
                    }
                }
            }

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
                MineDamaged = BarrierCount < 8 && MineLevel > 0;
                LastSol.RaidDamagedMine = MineDamaged;
                report += $"Incursión alienígena: perímetro {BarrierCount}/12, -{foodLoss} comida";
                report += MineDamaged ? "; mina averiada. " : "; mina protegida. ";
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
