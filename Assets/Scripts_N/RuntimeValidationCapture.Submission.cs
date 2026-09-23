using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

/// <summary>Opt-in player acceptance harness; inactive during ordinary use.</summary>
public sealed partial class RuntimeValidationCapture
{
    private string validationFolder;
    private int validationFailures;
    private int runtimeErrors;
    private readonly StringBuilder validationReport = new StringBuilder();

    private void Check(bool passed, string label)
    {
        validationReport.AppendLine((passed ? "PASS " : "FAIL ") + label);
        if (!passed) validationFailures++;
        FlushValidation();
    }
    private void FlushValidation() => File.WriteAllText(Path.Combine(validationFolder,"runtime-validation.txt"),validationReport.ToString());
    private void ObserveLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            runtimeErrors++;
            validationReport.AppendLine("RUNTIME_ERROR " + message);
        }
    }
    private Button NamedButton(string name) => FindObjectsByType<Button>(FindObjectsInactive.Include).FirstOrDefault(b=>b.name==name);
    private IEnumerator CaptureEvidence(string name)
    {
        yield return new WaitForSecondsRealtime(1f);
        ScreenCapture.CaptureScreenshot(Path.Combine(validationFolder,name+".png"));
        yield return new WaitForSecondsRealtime(1f);
    }
    private IEnumerator ValidateSubmission(string folder, string[] args)
    {
        validationFolder=Path.GetFullPath(folder);
        Directory.CreateDirectory(validationFolder);
        Application.runInBackground=true;
        Application.logMessageReceived+=ObserveLog;
        float duration=900f;
        for(int i=0;i<args.Length-1;i++) if(args[i]=="-ptmeoh-duration" && float.TryParse(args[i+1],out float parsed)) duration=Mathf.Max(30f,parsed);
        validationReport.AppendLine("UTC "+DateTime.UtcNow.ToString("O")+" Unity "+Application.unityVersion);
        validationReport.AppendLine("Programmatic UI callbacks/API checks; not physical mouse/keyboard or visual sign-off.");
        yield return new WaitForSecondsRealtime(8f);
        var sim=PlantProcessSimulator.Instance;
        validationReport.AppendLine("RESOLUTION "+Screen.width+"x"+Screen.height);
        Check(sim!=null,"Simulator startup");
        Check(FindObjectsByType<PlantProcessSimulator>().Length==1,"Single simulator owner");
        Check(FindObjectsByType<IcodosDashboardRuntime>().Length==1,"Single dashboard owner");
        Check(FindAnyObjectByType<SafetyWarningRuntime>()!=null,"Warning overlay startup");
        Check(FindObjectsByType<PipeFlowAnimator>().Length>=35,"Flow animator coverage");
        if(sim==null){Application.Quit(1);yield break;}
        foreach(string page in new[]{"OVERVIEW","PLANT PROCESS","FLOW LAB","REACTOR LAB","SIMULATION","ANALYTICS"})
        {
            var b=NamedButton(page);Check(b!=null && b.interactable,"Navigation exists "+page);
            if(b!=null)b.onClick.Invoke();
            yield return CaptureEvidence(page.Replace(' ','-').ToLowerInvariant());
        }
        var dashboard=IcodosDashboardRuntime.Instance;
        Check(dashboard!=null && dashboard.TryGetAnalyticsWindowScreenRect(out Rect analyticsRect) && analyticsRect.width>0,"Analytics split pane opens");
        NamedButton("ANALYTICS")?.onClick.Invoke();
        NamedButton("OVERVIEW")?.onClick.Invoke();
        var cameraController=FindAnyObjectByType<OrbitCameraController>();
        Check(cameraController!=null,"Camera controller");
        if(cameraController!=null)
        {
            cameraController.FocusModule(0);yield return new WaitForSecondsRealtime(1f);
            Check(cameraController.CurrentFocusIndex==0,"Camera module focus");
            cameraController.FocusNext();yield return new WaitForSecondsRealtime(1f);
            Check(cameraController.CurrentFocusIndex!=0,"Camera next module");
            cameraController.FocusOverview();
        }
        var water=FindObjectsByType<Slider>(FindObjectsInactive.Include).FirstOrDefault(s=>s.name=="Water feed Slider");
        Check(water!=null,"Water slider exists");
        if(water!=null)water.value=0;
        else sim.SetWaterFeed(0);
        yield return new WaitForSecondsRealtime(5f);
        Check(sim.Current.h2InputKgH<0.001f && sim.Current.oxygenByproductKgH<0.001f,"Runtime zero-water regression after display smoothing settles");
        yield return CaptureEvidence("zero-water");
        if(water!=null)water.value=100;
        sim.ResetSimulation();
        yield return new WaitForSecondsRealtime(3f);
        float before=sim.Current.reactorYieldPercent;
        sim.SetReactorTemperature(210);
        yield return new WaitForSecondsRealtime(3f);
        Check(Mathf.Abs(before-sim.Current.reactorYieldPercent)>0.01f,"Temperature changes live yield");
        sim.Pause();float stored=sim.Current.storedMethanolKg;
        yield return new WaitForSecondsRealtime(2f);
        Check(Mathf.Abs(stored-sim.Current.storedMethanolKg)<0.001f,"Pause freezes inventory");
        sim.Play();sim.ResetSimulation();
        yield return new WaitForSecondsRealtime(2f);
        Check(!sim.Current.storageInterlockActive,"Reset clears storage latch");
        string csv=MassBalanceCsvExporter.BuildCsv(sim);
        File.WriteAllText(Path.Combine(validationFolder,"player-mass-balance.csv"),csv);
        Check(csv.Contains("External closure") && csv.Contains("Assumption"),"Player mass-balance CSV generation and file write");
        Check(Mathf.Abs(sim.MassBalance.recycleFraction-sim.CurrentInputs.recycleRatio/100f)<0.00001f,"Player CSV current recycle basis");
        Check(sim.MassBalance.converged && sim.MassBalance.externalMassBalanceErrorPercent<0.001f,"Player CSV converged external closure below 0.001%");
        Check(Mathf.Abs(sim.MassBalance.freshCo2KgHr+sim.MassBalance.freshH2KgHr-sim.MassBalance.methanolProductKgHr-sim.MassBalance.waterProductKgHr-sim.MassBalance.purgeCo2KgHr-sim.MassBalance.purgeH2KgHr)<0.002f,"Player CSV external input/output stream closure");
        Check(sim.ApplyMaximumEfficiencyOperatingPoint(),"High-output preset applied");
        yield return new WaitForSecondsRealtime(5f);
        var samples=new StringBuilder("elapsed_seconds,frame_count,allocated_bytes,objects,storage_percent,interlock\n");
        float start=Time.realtimeSinceStartup;
        long startMemory=Profiler.GetTotalAllocatedMemoryLong();
        int startObjects=FindObjectsByType<GameObject>().Length;
        bool tripSeen=false,resetSeen=false;
        while(Time.realtimeSinceStartup-start<duration)
        {
            float elapsed=Time.realtimeSinceStartup-start;
            long memory=Profiler.GetTotalAllocatedMemoryLong();
            samples.AppendLine(string.Join(",",elapsed.ToString(System.Globalization.CultureInfo.InvariantCulture),Time.frameCount,memory,FindObjectsByType<GameObject>().Length,sim.Current.storageFillPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),sim.Current.storageInterlockActive));
            File.WriteAllText(Path.Combine(validationFolder,"soak.csv"),samples.ToString());
            if(sim.Current.storageInterlockActive && !tripSeen)
            {
                tripSeen=true;yield return new WaitForSecondsRealtime(5f);
                Check(sim.Current.methanolProductionKgH<0.01f,"Natural storage high-high trip stops production");
                yield return CaptureEvidence("storage-trip");
                sim.ResetStoredMethanol();yield return new WaitForSecondsRealtime(3f);
                resetSeen=!sim.Current.storageInterlockActive && sim.Current.methanolProductionKgH>0;
                Check(resetSeen,"Storage reset restores production");
            }
            yield return new WaitForSecondsRealtime(15f);
        }
        long endMemory=Profiler.GetTotalAllocatedMemoryLong();
        int endObjects=FindObjectsByType<GameObject>().Length;
        Check(FindObjectsByType<PlantProcessSimulator>().Length==1 && FindObjectsByType<IcodosDashboardRuntime>().Length==1,"No duplicate runtime owners after soak");
        Check(endObjects<=startObjects+20,"Bounded object count (allowance 20 transient objects)");
        Check(endMemory-startMemory<Math.Max(32L*1024*1024,startMemory/5),"Allocated-memory growth below coarse 32 MiB/20% envelope; not proof of zero leaks");
        Check(runtimeErrors==0,"No observed runtime errors/exceptions");
        if(duration>=900){Check(tripSeen,"Natural storage trip observed during 15-minute soak");Check(resetSeen,"Natural trip/reset cycle completed");}
        validationReport.AppendLine("SOAK_SECONDS "+(Time.realtimeSinceStartup-start));
        validationReport.AppendLine("MEMORY_BYTES start="+startMemory+" end="+endMemory+" OBJECTS start="+startObjects+" end="+endObjects);
        validationReport.AppendLine(validationFailures==0 ? "RUNTIME_VALIDATION_PASS" : "RUNTIME_VALIDATION_FAIL count="+validationFailures);
        FlushValidation();
        Application.logMessageReceived-=ObserveLog;
        Application.Quit(validationFailures==0?0:1);
    }
}
