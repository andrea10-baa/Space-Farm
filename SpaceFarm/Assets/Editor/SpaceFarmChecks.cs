using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaceFarm.Editor
{
    public static class SpaceFarmChecks
    {
        private static readonly int[] FixedSeeds = { 1, 42, 12345 };

        [MenuItem("Space Farm/Comprobar reglas básicas")]
        public static void Run()
        {
            CheckColonyRules();
            CheckSeveralMines();
            CheckGeneration();
            Debug.Log("SPACE_FARM_CHECKS_OK: mina, almacén, rover, barreras, incursión, reportes, varias minas y generación de mapas.");
        }

        private static void CheckColonyRules()
        {
            SpaceFarmState colony = new SpaceFarmState();
            Require(colony.Population == 2 && colony.OxygenPlants == 1, "Estado inicial");
            Require(colony.AddColonist(out _), "La primera planta debe sostener a tres colonos");
            Require(!colony.AddColonist(out _), "La reserva de O₂ debe impedir un cuarto colono");
            Require(!colony.BuildMine(0, out _), "No se construye sobre un yacimiento sin estudiar");
            Require(colony.SurveyDeposit(0).Contains("Fe-1"), "El rover debe estudiar hierro");
            Require(!colony.BuildMine(1, out _), "Una mina no se construye sobre hielo");
            Require(colony.BuildMine(0, out _), "La mina debe poder construirse tras el estudio");
            int ironAfterBuild = colony.Iron;
            colony.AdvanceSol();
            SpaceFarmState.DepositState mine = colony.Deposit(0);
            Require(colony.Iron == ironAfterBuild, "La mina no entrega hierro al inventario");
            Require(mine.MineStored == 3 && mine.LastResult.Actual == 3 && mine.LastResult.EnergyUsed == 2 &&
                colony.LastSol.Mine.Actual == 3, "Producción real y consumo de mina nivel 1");
            colony.AdvanceSol();
            colony.AdvanceSol();
            Require(mine.MineStored == 6 && mine.LastResult.Actual == 0 &&
                mine.LastResult.Reason.Contains("lleno"), "Almacén lleno detiene la extracción");
            Require(colony.LoadMineToRover(0, out _) && colony.RoverCargo == 4 &&
                mine.MineStored == 2 && colony.Iron == ironAfterBuild, "Carga inicial del rover");
            Require(!colony.LoadMineToRover(0, out _) && mine.MineStored == 2, "Un rover cargado no vuelve a cargar");
            Require(colony.UnloadRover(out _) && colony.Iron == ironAfterBuild + 4 &&
                colony.RoverCargo == 0, "Descarga en el hábitat");
            Require(!colony.UnloadRover(out _) && colony.Iron == ironAfterBuild + 4, "Descargar dos veces no duplica hierro");
            Require(colony.UpgradeRover(out _) && colony.RoverCapacity == 8, "Capacidad del rover mejorado");
            Require(colony.UpgradeMine(0, out _) && mine.MineCapacity == 15, "Capacidad de mina mejorada");
            int beforeBarrier = colony.Iron;
            Require(colony.BuildBarrierSegment(0, out _) && colony.Iron == beforeBarrier - 1,
                "Cada segmento cuesta un hierro");
            Require(!colony.BuildBarrierSegment(0, out _) && colony.Iron == beforeBarrier - 1,
                "Un segmento repetido no cobra");
            Require(!colony.BuildBarrierSegment(-1, out _) && colony.Iron == beforeBarrier - 1,
                "Una posición inválida no cobra");
            Require(RaidWith(2).LastSol.RaidFoodLoss == 4 && RaidWith(2).AnyMineDamaged, "Defensa 0-3");
            Require(RaidWith(5).LastSol.RaidFoodLoss == 3 && RaidWith(5).AnyMineDamaged, "Defensa 4-7");
            Require(RaidWith(9).LastSol.RaidFoodLoss == 2 && !RaidWith(9).AnyMineDamaged, "Defensa 8-11");
            Require(RaidWith(12).LastSol.RaidFoodLoss == 1 && !RaidWith(12).AnyMineDamaged, "Defensa completa");
            SpaceFarmState discovery = new SpaceFarmState();
            int iron = discovery.Iron;
            discovery.ClaimDiscovery(0);
            discovery.ClaimDiscovery(0);
            Require(discovery.Iron == iron + 2, "Recompensa de una sola vez");

            SpaceFarmState mission = new SpaceFarmState();
            mission.SurveyDeposit(0);
            mission.SurveyDeposit(1);
            Require(mission.BuildMine(0, out _), "Mina para la misión");
            for (int i = 0; i < 8; i++) Require(mission.BuildBarrierSegment(i, out _), "Defensa para la misión");
            Require(mission.AddColonist(out _), "Colono para la misión");
            mission.AdvanceSol();
            Require(mission.LoadMineToRover(0, out _) && mission.UnloadRover(out _), "Primera cosecha de hierro");
            Require(mission.BuildGreenhouse(out _), "Invernadero para la misión");
            mission.AdvanceSol();
            Require(mission.LoadMineToRover(0, out _) && mission.UnloadRover(out _), "Segunda cosecha de hierro");
            mission.AdvanceSol();
            Require(mission.LoadMineToRover(0, out _) && mission.UnloadRover(out _), "Tercera cosecha de hierro");
            Require(mission.BuildWaterExtractor(1, out _), "Extractor para la misión");
            while (mission.Sol < 7) mission.AdvanceSol();
            Require(mission.MissionComplete, "La misión sigue siendo completable tras el cambio de economía");
            SpaceFarmState loss = new SpaceFarmState();
            while (!loss.GameOver && loss.Sol < 14) loss.AdvanceSol();
            Require(loss.GameOver && !loss.MissionComplete, "El agotamiento de recursos termina la misión");
        }

        /// <summary>Several deposits: each mine keeps its own level, damage and storage, and the operator rule limits how many run.</summary>
        private static void CheckSeveralMines()
        {
            List<MapDeposit> layout = new List<MapDeposit>
            {
                new MapDeposit { Kind = DepositKind.Iron, Distance = 6, Name = "Fe-1" },
                new MapDeposit { Kind = DepositKind.Iron, Distance = 20, Name = "Fe-2" },
                new MapDeposit { Kind = DepositKind.Iron, Distance = 31, Name = "Fe-3" },
                new MapDeposit { Kind = DepositKind.Ice, Distance = 7, Name = "Hielo-1" },
                new MapDeposit { Kind = DepositKind.Ice, Distance = 18, Name = "Hielo-2" },
                new MapDeposit { Kind = DepositKind.Ice, Distance = 26, Name = "Hielo-3" }
            };
            SpaceFarmState colony = new SpaceFarmState(layout, new[] { 0, 1, 2, 3 });
            int start = colony.Iron;
            colony.SurveyDeposit(0);
            colony.SurveyDeposit(1);
            Require(colony.Iron == start + 3, "La primera muestra trae 2 Fe y las siguientes 1");
            Require(colony.MaxMines == 1 && colony.MineCost == 8, "Dos colonos operan una mina");
            Require(colony.BuildMine(0, out _), "Primera mina");
            Require(!colony.BuildMine(1, out string blocked) && blocked.Contains("operador"), "La segunda mina exige otro colono");
            Require(colony.AddColonist(out _) && colony.MaxMines == 2 && colony.MineCost == 12, "El tercer colono habilita otra mina, más cara");
            int beforeSecond = colony.Iron;
            Require(colony.BuildMine(1, out _) && colony.Iron == beforeSecond - 12, "Segunda mina a 12 Fe");
            Require(colony.MineCount == 2, "Dos minas activas");

            colony.AdvanceSol();
            SpaceFarmState.DepositState near = colony.Deposit(0);
            SpaceFarmState.DepositState far = colony.Deposit(1);
            Require(near.MineStored == 3 && far.MineStored == 3 && colony.LastSol.Mine.Actual == 6 &&
                colony.LastSol.Mine.EnergyUsed == 4, "Cada mina produce y consume por separado");
            Require(colony.LoadMineToRover(1, out _) && far.MineStored == 0 && near.MineStored == 3,
                "Recoger de una mina no toca el almacén de la otra");
            Require(colony.UnloadRover(out _), "Descarga de la mina lejana");
            Require(colony.LoadMineToRover(0, out _) && colony.UnloadRover(out _), "Descarga de la mina cercana");
            for (int i = 0; i < 4; i++) Require(colony.BuildBarrierSegment(i, out _), "Barreras parciales");
            colony.AdvanceSol();
            Require(colony.LoadMineToRover(0, out _) && colony.UnloadRover(out _) &&
                colony.LoadMineToRover(1, out _) && colony.UnloadRover(out _), "Segunda ronda de recogida");
            Require(colony.UpgradeMine(0, out _) && near.MineLevel == 2 && far.MineLevel == 1,
                "Mejorar una mina no mejora la otra");

            // Sol 5 raid with 4-7 segments: only the farthest mine is hit.
            while (colony.Sol < 5) colony.AdvanceSol();
            Require(far.MineDamaged && !near.MineDamaged && colony.LastSol.RaidDamagedMines == 1,
                "Con 4-7 segmentos solo se avería la mina más lejana");
            int nearStored = near.MineStored;
            int farStored = far.MineStored;
            colony.AdvanceSol();
            Require(far.MineStored == farStored && far.LastResult.Actual == 0 && near.MineStored >= nearStored,
                "La mina averiada no produce y la sana sigue");
            if (colony.Iron < 3) { colony.LoadMineToRover(0, out _); colony.UnloadRover(out _); }
            Require(colony.RepairMine(1, out _) && !far.MineDamaged && near.MineLevel == 2, "Reparar una mina concreta");

            // Extractors: independent, capped at two, second one costs more.
            SpaceFarmState water = new SpaceFarmState(layout, new[] { 0 });
            water.SurveyDeposit(3);
            water.SurveyDeposit(4);
            water.SurveyDeposit(5);
            Require(water.ExtractorCost == 6 && water.BuildWaterExtractor(3, out _), "Primer extractor");
            Require(water.ExtractorCost == 10 && water.BuildWaterExtractor(4, out _), "Segundo extractor a 10 Fe");
            Require(!water.BuildWaterExtractor(5, out _) && water.ExtractorCount == 2, "Máximo de dos extractores");
            Require(!water.BuildMine(3, out _), "No hay mina sobre hielo");
        }

        private static void CheckGeneration()
        {
            foreach (int seed in FixedSeeds)
            {
                SpaceFarmMap first = SpaceFarmMap.Generate(seed);
                SpaceFarmMap again = SpaceFarmMap.Generate(seed);
                Require(first.Signature == again.Signature && first.Describe() == again.Describe(),
                    $"La semilla {seed} debe repetir exactamente el mismo mapa");
                Require(first.Validate(out string error), $"Semilla {seed} inválida: {error}");
                RequireLayout(first);
                Debug.Log("SPACE_FARM_SEED: " + first.Describe());
            }
            SpaceFarmMap a = SpaceFarmMap.Generate(1);
            SpaceFarmMap b = SpaceFarmMap.Generate(42);
            SpaceFarmMap c = SpaceFarmMap.Generate(12345);
            Require(a.Signature != b.Signature && b.Signature != c.Signature && a.Signature != c.Signature,
                "Semillas distintas deben dar mapas distintos");
            Require(DifferentDeposits(a, b) >= a.Deposits.Count / 2 && DifferentDeposits(b, c) >= b.Deposits.Count / 2,
                "Semillas distintas deben mover la mayoría de los yacimientos");

            // Sweep: every seed must give a valid, early-playable layout.
            HashSet<ulong> signatures = new HashSet<ulong>();
            int retries = 0;
            for (int seed = -50; seed < 450; seed++)
            {
                SpaceFarmMap map = SpaceFarmMap.Generate(seed);
                Require(map.Validate(out string error), $"Semilla {seed} inválida: {error}");
                RequireLayout(map);
                signatures.Add(map.Signature);
                if (map.Attempt > 0) retries++;
            }
            Require(signatures.Count == 500, "Cada semilla del barrido debe producir un mapa propio");
            Debug.Log($"SPACE_FARM_SWEEP: 500 semillas válidas, {retries} necesitaron regenerarse.");

            // A real session state built from a generated map keeps one entry per deposit.
            SpaceFarmState session = new SpaceFarmState(a);
            Require(session.Deposits.Count == a.Deposits.Count && session.DiscoveryCount == a.Points.Count,
                "El estado refleja los yacimientos y puntos del mapa");
        }

        private static void RequireLayout(SpaceFarmMap map)
        {
            string tag = $"Semilla {map.Seed}: ";
            HashSet<Vector2Int> used = new HashSet<Vector2Int>();
            // Cells covered by the base buildings and the 12 barrier slots (see SpaceFarmGame.BarrierOffset).
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
                used.Add(map.BaseCell + new Vector2Int(dx, dy));
            foreach (MapDeposit deposit in map.Deposits)
            {
                Require(map.Inside(deposit.Cell), tag + "yacimiento fuera del mapa");
                Require(used.Add(deposit.Cell), tag + $"{deposit.Name} se superpone con otro elemento o con la base");
                Require(deposit.Distance == SpaceFarmMap.CellDistance(deposit.Cell, map.BaseCell), tag + "distancia incoherente");
            }
            foreach (MapPoint point in map.Points)
            {
                Require(map.Inside(point.Cell), tag + "punto de interés fuera del mapa");
                Require(used.Add(point.Cell), tag + "punto de interés superpuesto");
            }
            Require(map.Nearest(DepositKind.Iron).Distance <= SpaceFarmMap.NearIronMaxDistance, tag + "sin hierro temprano");
            Require(map.Nearest(DepositKind.Ice).Distance <= SpaceFarmMap.NearIceMaxDistance, tag + "sin hielo temprano");
            int iron = map.Count(DepositKind.Iron);
            int ice = map.Count(DepositKind.Ice);
            Require(iron >= 6 && iron <= 10 && ice >= 4 && ice <= 6 && map.Points.Count >= 4 && map.Points.Count <= 6,
                tag + "cantidades fuera de rango");
            bool big = false;
            foreach (MapCrater crater in map.Craters)
            {
                big |= crater.Radius >= 5f;
                float distance = Vector2.Distance(new Vector2(crater.X, crater.Y), map.BaseCell);
                Require(distance > crater.Radius * 1.45f + 4f, tag + "cráter sobre la base o sus barreras");
            }
            Require(big && map.Craters.Count >= 8, tag + "falta el cráter grande o los menores");
            Require(map.Terrain[map.BaseCell.x, map.BaseCell.y] == TerrainKind.Plain, tag + "la base no está en terreno despejado");
        }

        private static int DifferentDeposits(SpaceFarmMap a, SpaceFarmMap b)
        {
            int different = 0;
            foreach (MapDeposit deposit in a.Deposits)
            {
                MapDeposit other = b.DepositAt(deposit.Cell);
                if (other == null || other.Kind != deposit.Kind) different++;
            }
            return different;
        }

        private static SpaceFarmState RaidWith(int segments)
        {
            SpaceFarmState raid = new SpaceFarmState();
            raid.SurveyDeposit(0);
            Require(raid.BuildMine(0, out _), "Preparación de mina para incursión");
            for (int i = 0; i < segments; i++) Require(raid.BuildBarrierSegment(i, out _), "Construcción de defensa");
            for (int i = 0; i < 4; i++) raid.AdvanceSol();
            return raid;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("SPACE_FARM_CHECK_FAILED: " + message);
        }
    }
}
