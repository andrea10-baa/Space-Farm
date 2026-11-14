using System;
using UnityEngine;

namespace SpaceFarm
{
    /// <summary>Small deterministic simulation for the first playable colony loop.</summary>
    [Serializable]
    public sealed class SpaceFarmState
    {
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
        public bool Barrier { get; private set; }
        public bool MineDamaged { get; private set; }
        public int ShipmentArrivalSol { get; private set; }
        public bool GameOver => Health <= 0;
        public bool MissionComplete => Sol >= 7 && MineLevel > 0 && Greenhouse && Population >= 3 && Barrier && !GameOver;
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
            return Success("Mina nivel 1 construida. Produce 3 de hierro por sol y consume 2 de energía.", out message);
        }

        public bool UpgradeMine(out string message)
        {
            if (MineLevel == 0) return Fail("Construí primero la mina.", out message);
            if (MineLevel >= 2) return Fail("El nivel 2 es el máximo de esta demo.", out message);
            if (MineDamaged) return Fail("Repará la mina antes de mejorarla.", out message);
            if (Iron < 6) return Fail("La mejora necesita 6 de hierro.", out message);
            Iron -= 6;
            MineLevel = 2;
            return Success("Mina nivel 2 lista. Produce 5 de hierro por sol; requiere 3 de energía.", out message);
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

        public bool BuildBarrier(out string message)
        {
            if (Barrier) return Fail("El perímetro ya tiene barreras.", out message);
            if (Iron < 5) return Fail("Las barreras necesitan 5 de hierro.", out message);
            Iron -= 5;
            Barrier = true;
            return Success("Barreras instaladas: reducirán los daños de una incursión.", out message);
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
            Energy = Mathf.Min(30, Energy + solar);
            string report = $"Sol {Sol}: los paneles generaron {solar} de energía";
            if (storm) report += " por la tormenta de polvo";
            report += ". ";

            int oxygenMade = 0;
            for (int i = 0; i < OxygenPlants; i++)
            {
                if (SpendEnergy(3)) oxygenMade += 4;
            }
            Oxygen = Mathf.Min(30, Oxygen + oxygenMade);
            report += $"Plantas de O2: +{oxygenMade}. ";

            if (WaterExtractor && SpendEnergy(2))
            {
                Water = Mathf.Min(30, Water + 5);
                report += "Extractor de hielo: +5 agua. ";
            }
            if (Greenhouse && Water > 0 && SpendEnergy(2))
            {
                Water--;
                Food = Mathf.Min(30, Food + 4);
                report += "Invernadero: -1 agua, +4 comida. ";
            }
            if (MineLevel > 0 && !MineDamaged && SpendEnergy(MineLevel == 1 ? 2 : 3))
            {
                int produced = MineLevel == 1 ? 3 : 5;
                Iron += produced;
                report += $"Mina: +{produced} hierro. ";
            }

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
                if (Barrier)
                {
                    Food = Mathf.Max(0, Food - 1);
                    report += "Incursión alienígena: las barreras resistieron; -1 comida. ";
                }
                else
                {
                    Food = Mathf.Max(0, Food - 4);
                    MineDamaged = MineLevel > 0;
                    report += "Incursión alienígena: -4 comida; la mina quedó averiada. ";
                }
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
