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
            Require(colony.Iron > ironAfterBuild, "La mina debe producir hierro");

            SpaceFarmState raid = new SpaceFarmState();
            raid.SurveyIron();
            Require(raid.BuildMine(out _), "Preparación de mina para incursión");
            for (int i = 0; i < 4; i++) raid.AdvanceSol();
            Require(raid.MineDamaged, "La incursión sin barreras debe averiar la mina");
            Require(raid.RepairMine(out _), "La mina debe poder repararse");

            SpaceFarmState mission = new SpaceFarmState();
            mission.SurveyIron();
            mission.SurveyIce();
            Require(mission.BuildMine(out _), "Preparar la mina");
            Require(mission.BuildWaterExtractor(out _), "Preparar el extractor");
            Require(mission.BuildGreenhouse(out _), "Preparar el invernadero");
            for (int i = 0; i < 3; i++) mission.AdvanceSol();
            Require(mission.BuildBarrier(out _), "Las barreras deben ser asequibles antes de la incursión");
            mission.AdvanceSol();
            Require(!mission.MineDamaged, "Las barreras deben proteger la mina");
            Require(mission.AddColonist(out _), "La colonia debe poder crecer a tres personas");
            mission.AdvanceSol();
            mission.AdvanceSol();
            Require(mission.MissionComplete, "La misión debe poder completarse en el sol 7");

            Debug.Log("SPACE_FARM_CHECKS_OK: oxígeno, exploración, mina, incursión y victoria.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("SPACE_FARM_CHECK_FAILED: " + message);
        }
    }
}
