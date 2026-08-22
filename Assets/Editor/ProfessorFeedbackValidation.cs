using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Regression checks for the professor-requested reactor parameter sensitivity.</summary>
public static class ProfessorFeedbackValidation
{
    [MenuItem("Tools/Nived/Validate Professor Feedback Controls")]
    public static void Run()
    {
        GameObject testObject = new GameObject("Professor feedback validation");
        try
        {
            PlantProcessSimulator simulator = testObject.AddComponent<PlantProcessSimulator>();
            MethodInfo awake = typeof(PlantProcessSimulator).GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            awake?.Invoke(simulator, null);
            MethodInfo calculate = typeof(PlantProcessSimulator).GetMethod(
                "CalculateSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            if (calculate == null) throw new InvalidOperationException("CalculateSnapshot was not found.");

            simulator.SetReactorPressure(70f);
            simulator.SetH2Co2Ratio(3f);
            simulator.SetGHSV(8000f);
            simulator.SetReactorFeedFlow(100f);
            simulator.SetRecycleRatio(65f);

            simulator.SetReactorTemperature(210f);
            var lowTemperature = Snapshot(calculate, simulator);
            simulator.SetReactorTemperature(255f);
            var designTemperature = Snapshot(calculate, simulator);
            RequireDifferent(lowTemperature.reactorYieldPercent, designTemperature.reactorYieldPercent, "temperature");

            simulator.SetReactorPressure(40f);
            var lowPressure = Snapshot(calculate, simulator);
            simulator.SetReactorPressure(100f);
            var highPressure = Snapshot(calculate, simulator);
            RequireDifferent(lowPressure.reactorYieldPercent, highPressure.reactorYieldPercent, "pressure");

            simulator.SetReactorPressure(70f);
            simulator.SetH2Co2Ratio(2f);
            var offRatio = Snapshot(calculate, simulator);
            simulator.SetH2Co2Ratio(3f);
            var stoichiometricRatio = Snapshot(calculate, simulator);
            RequireDifferent(offRatio.reactorYieldPercent, stoichiometricRatio.reactorYieldPercent, "H2/CO2 ratio");

            simulator.SetReactorFeedFlow(50f);
            var lowFlow = Snapshot(calculate, simulator);
            simulator.SetReactorFeedFlow(100f);
            var fullFlow = Snapshot(calculate, simulator);
            RequireDifferent(lowFlow.methanolProductionKgH, fullFlow.methanolProductionKgH, "feed flow");

            Debug.Log("PROFESSOR_FEEDBACK_VALIDATION_OK temperature=responsive pressure=responsive ratio=responsive flow=responsive constants=held");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }
    }

    private static PlantProcessSimulator.ProcessSnapshot Snapshot(MethodInfo method, PlantProcessSimulator simulator)
    {
        return (PlantProcessSimulator.ProcessSnapshot)method.Invoke(simulator, null);
    }

    private static void RequireDifferent(float first, float second, string parameter)
    {
        if (Mathf.Abs(first - second) < 0.01f)
            throw new InvalidOperationException($"Changing {parameter} did not change the calculated result.");
    }
}
