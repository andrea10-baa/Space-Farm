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
            SpaceFarmState colony = new SpaceFarmState();
            Require(colony.Population == 2 && colony.OxygenPlants == 1, "Estado inicial");
            Require(colony.AddColonist(out _), "La primera planta debe sostener a tres colonos");
            Require(!colony.AddColonist(out _), "La reserva de O₂ debe impedir un cuarto colono");
            Require(colony.SurveyIron().Contains("hierro"), "El rover debe estudiar hierro");
            Require(colony.BuildMine(out _), "La mina debe poder construirse tras el estudio");
            int ironAfterBuild = colony.Iron;
            colony.AdvanceSol();
            Require(colony.Iron == ironAfterBuild, "La mina no entrega hierro al inventario");
            Require(colony.MineStored == 3 && colony.LastSol.Mine.Actual == 3 &&
                colony.LastSol.Mine.EnergyUsed == 2, "Producción real y consumo de mina nivel 1");
            colony.AdvanceSol();
            colony.AdvanceSol();
            Require(colony.MineStored == 6 && colony.LastSol.Mine.Actual == 0 &&
                colony.LastSol.Mine.Reason.Contains("lleno"), "Almacén lleno detiene la extracción");
            Require(colony.LoadMineToRover(out _) && colony.RoverCargo == 4 &&
                colony.MineStored == 2 && colony.Iron == ironAfterBuild, "Carga inicial del rover");
            Require(colony.UnloadRover(out _) && colony.Iron == ironAfterBuild + 4 &&
                colony.RoverCargo == 0, "Descarga en el hábitat");
            Require(colony.UpgradeRover(out _) && colony.RoverCapacity == 8, "Capacidad del rover mejorado");
            Require(colony.UpgradeMine(out _) && colony.MineCapacity == 15, "Capacidad de mina mejorada");
            int beforeBarrier = colony.Iron;
            Require(colony.BuildBarrierSegment(0, out _) && colony.Iron == beforeBarrier - 1,
                "Cada segmento cuesta un hierro");
            Require(!colony.BuildBarrierSegment(0, out _) && colony.Iron == beforeBarrier - 1,
                "Un segmento repetido no cobra");
            Require(!colony.BuildBarrierSegment(-1, out _) && colony.Iron == beforeBarrier - 1,
                "Una posición inválida no cobra");
            Require(RaidWith(2).LastSol.RaidFoodLoss == 4 && RaidWith(2).MineDamaged,
                "Defensa 0-3");
            Require(RaidWith(5).LastSol.RaidFoodLoss == 3 && RaidWith(5).MineDamaged,
                "Defensa 4-7");
            Require(RaidWith(9).LastSol.RaidFoodLoss == 2 && !RaidWith(9).MineDamaged,
                "Defensa 8-11");
            Require(RaidWith(12).LastSol.RaidFoodLoss == 1 && !RaidWith(12).MineDamaged,
                "Defensa completa");
            SpaceFarmState discovery = new SpaceFarmState();
            int iron = discovery.Iron;
            discovery.ClaimDiscovery(0);
            discovery.ClaimDiscovery(0);
            Require(discovery.Iron == iron + 2, "Recompensa de una sola vez");
            SpaceFarmState mission = new SpaceFarmState();
            mission.SurveyIron();
            mission.SurveyIce();
            Require(mission.BuildMine(out _), "Mina para la misión");
            for (int i = 0; i < 8; i++) Require(mission.BuildBarrierSegment(i, out _), "Defensa para la misión");
            Require(mission.AddColonist(out _), "Colono para la misión");
            mission.AdvanceSol();
            Require(mission.LoadMineToRover(out _) && mission.UnloadRover(out _), "Primera cosecha de hierro");
            Require(mission.BuildGreenhouse(out _), "Invernadero para la misión");
            mission.AdvanceSol();
            Require(mission.LoadMineToRover(out _) && mission.UnloadRover(out _), "Segunda cosecha de hierro");
            mission.AdvanceSol();
            Require(mission.LoadMineToRover(out _) && mission.UnloadRover(out _), "Tercera cosecha de hierro");
            Require(mission.BuildWaterExtractor(out _), "Extractor para la misión");
            while (mission.Sol < 7) mission.AdvanceSol();
            Require(mission.MissionComplete, "La misión sigue siendo completable tras el cambio de economía");
            SpaceFarmState loss = new SpaceFarmState();
            while (!loss.GameOver && loss.Sol < 14) loss.AdvanceSol();
            Require(loss.GameOver && !loss.MissionComplete, "El agotamiento de recursos termina la misión");
            Debug.Log("SPACE_FARM_CHECKS_OK: mina, almacén, rover, barreras, incursión y reportes.");
        }

        private static SpaceFarmState RaidWith(int segments)
        {
            SpaceFarmState raid = new SpaceFarmState();
            raid.SurveyIron();
            Require(raid.BuildMine(out _), "Preparación de mina para incursión");
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
