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
            CheckEconomyAndWalls();
            Debug.Log("SPACE_FARM_CHECKS_OK: zonas con recursos, cinco niveles, trabajadores, descanso, mejoras temporizadas, economía y muros libres.");
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
            Require(state.Deposit(0).MineStored == 4 && state.Deposit(0).LastResult.Nominal == 4,
                "un trabajador activo acelera la mina");
            state.Tick(48f);
            Require(state.Crew[0].Resting && state.ActiveWorkers(CrewJob.Mine, 0) == 0,
                "el astronauta agotado se retira a dormir");
            state.Tick(17f);
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
            Require(state.UpgradeRover(out _) && state.RoverCapacity == 4, "rover en mejora conserva su capacidad");
            state.Tick(30f);
            Require(state.RoverCapacity == 8, "rover mejorado carga más hierro");
            SpaceFarmState solar = new SpaceFarmState();
            Require(solar.UpgradeSolar(out _) && solar.SolarLevel == 1, "paneles en mejora");
            solar.Tick(30f);
            Require(solar.SolarLevel == 2 && solar.SolarPerSol == 21 && solar.EnergyCapacity == 40,
                "paneles mejorados aumentan generación y batería");
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
            Require(economy.PlaceStructure(StructureKind.Greenhouse, new Vector2Int(10, 10), true, out _),
                "primer invernadero");
            Require(economy.PlaceStructure(StructureKind.OxygenPlant, new Vector2Int(20, 20), true, out _),
                "segunda planta de oxígeno");
            economy.AdvanceSol();
            Require(economy.LastSol.CrewConsumed == 6 && economy.Oxygen == 14 && economy.Food == 12 &&
                economy.Water == 7, "producción y consumo crecen con la población y edificios");
            Require(economy.LastSol.OxygenUnits.Count == 2 && economy.LastSol.GreenhouseUnits.Count == 1,
                "cada instalación presenta su propia producción");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("SPACE_FARM_CHECK_FAILED: " + message);
        }
    }
}
