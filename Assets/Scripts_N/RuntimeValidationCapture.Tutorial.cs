using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Synthetic input exercises Unity's input path. Physical device/visual sign-off stays separate.
public sealed partial class RuntimeValidationCapture
{
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static Rect ScreenRect(RectTransform rect)
    {
        var corners=new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    private IEnumerator ValidateTutorial()
    {
        var tour=TutorialRuntime.Instance;
        Check(tour!=null && tour.IsRunning && tour.CurrentStepIndex==0,"Tutorial first-launch path (isolated preference override)");
        if(tour==null)yield break;
        PlantProcessSimulator.Instance.Pause();
        var inputBefore=JsonUtility.ToJson(PlantProcessSimulator.Instance.CurrentInputs);
        Button next=tour.GetComponentsInChildren<Button>(true).First(b=>b.name=="Next Step");
        Button prev=tour.GetComponentsInChildren<Button>(true).First(b=>b.name=="Previous Step");
        Button skip=tour.GetComponentsInChildren<Button>(true).First(b=>b.name=="Skip Tutorial");
        Check(!prev.interactable,"Tutorial PREVIOUS disabled on first step");
        next.onClick.Invoke(); Check(tour.CurrentStepIndex==1,"Tutorial NEXT");
        prev.onClick.Invoke(); Check(tour.CurrentStepIndex==0,"Tutorial PREVIOUS");
        skip.onClick.Invoke(); yield return new WaitForSecondsRealtime(.4f);
        Check(!tour.IsRunning,"Tutorial SKIP");
        NamedButton("Help")?.onClick.Invoke(); yield return new WaitForSecondsRealtime(.4f);
        Check(NamedButton("Start Tutorial")?.gameObject.activeInHierarchy==true,"HELP offers START TUTORIAL");
        NamedButton("Start Tutorial")?.onClick.Invoke(); yield return new WaitForSecondsRealtime(.5f);
        Check(tour.IsRunning && tour.CurrentStepIndex==0,"HELP tutorial replay");
        Check(!Field<bool>(IcodosDashboardRuntime.Instance,"helpPanelOpen"),"Replay clears help modal state");
        for(int i=0;i<tour.StepCount;i++)
        {
            Check(tour.CurrentStepIndex==i,"Tutorial step index "+i);
            yield return new WaitForSecondsRealtime(.5f);
            var target=Field<RectTransform>(tour,"currentTarget");
            if(i!=0 && i!=8 && i!=9 && i!=22)Check(target!=null,"Tutorial target resolved "+i);
            var card=Field<RectTransform>(tour,"card");
            Rect bounds=ScreenRect(card);
            Check(bounds.xMin>=-1 && bounds.yMin>=-1 && bounds.xMax<=Screen.width+1 && bounds.yMax<=Screen.height+1,"Tutorial card inside viewport "+i);
            var body=Field<Text>(tour,"cardBody");
            Check(body.preferredHeight<=body.rectTransform.rect.height+1,"Tutorial body fits card "+i);
            if(i==8)yield return ValidateTutorialMouse(tour);
            if(i==10)ValidateProcessLayout();
            if(i==17)yield return ValidateAutomaticOfat();
            if(i==20)
            {
                var arrow=Field<GameObject>(tour,"arrowRoot");
                Check(arrow.activeInHierarchy,"Warning-band arrow visible");
                Check(target!=null && target.name=="Warning Panel","Warning arrow targets actual warning strip");
                var mesh=arrow.GetComponentInChildren<TutorialArrowHead>().canvasRenderer.GetMesh();
                Check(mesh!=null && mesh.vertexCount>=3,"Warning arrowhead emits triangle mesh");
            }
            yield return CaptureEvidence("tutorial-"+i.ToString("D2"));
            next.onClick.Invoke();
        }
        yield return new WaitForSecondsRealtime(.4f);
        Check(!tour.IsRunning,"Tutorial FINISH");
        Check(!Field<bool>(IcodosDashboardRuntime.Instance,"analyticsWindowOpen"),"Tutorial restores closed analytics");
        Check(inputBefore==JsonUtility.ToJson(PlantProcessSimulator.Instance.CurrentInputs),"Tutorial leaves all process inputs unchanged");
        Check(!PlantProcessSimulator.Instance.IsRunning,"Tutorial preserves paused state");
        PlantProcessSimulator.Instance.Play();
        validationReport.AppendLine("Tutorial input: synthetic Unity Input System mouse/keyboard events, not physical hardware sign-off.");
    }
    private IEnumerator ValidateAutomaticOfat()
    {
        var dashboard=IcodosDashboardRuntime.Instance;
        Check(dashboard.TryGetAnalyticsWindowScreenRect(out Rect dock) && dock.xMin>=Screen.width*.5f,"Analytics retains right split pane");
        var graph=FindAnyObjectByType<OfatTimelineGraphRuntime>();
        Check(graph!=null,"Current automatic OFAT component exists");
        if(graph==null)yield break;
        var before=JsonUtility.ToJson(PlantProcessSimulator.Instance.CurrentInputs);
        var buttons=Field<Button[]>(graph,"varButtons");
        string[] lists={"temperatureSweepPoints","pressureSweepPoints","ratioSweepPoints","ghsvSweepPoints","feedSweepPoints"};
        for(int i=1;i<=5;i++)
        {
            buttons[i].onClick.Invoke();yield return null;
            var points=Field<System.Collections.IList>(graph,lists[i-1]);
            Check(points.Count==13,"Automatic OFAT 13 points "+lists[i-1]);
            Check(before==JsonUtility.ToJson(PlantProcessSimulator.Instance.CurrentInputs),"Automatic OFAT nonmutation "+lists[i-1]);
            yield return CaptureEvidence("ofat-"+i);
        }
        buttons[0].onClick.Invoke();
    }
    private void ValidateProcessLayout()
    {
        var panel=FindObjectsByType<RectTransform>(FindObjectsInactive.Include).First(t=>t.name=="Guided Process");
        string[] names={"Panel Title","Step","Process Title","Explanation","Streams","Previous Step","Next Step"};
        var rects=names.Select(n=>panel.Find(n).GetComponent<RectTransform>()).ToArray();
        for(int i=0;i<rects.Length;i++)
        {
            Check(rects[i].rect.width>0 && rects[i].rect.height>0,"Process Map positive layout "+names[i]);
            for(int j=i+1;j<rects.Length;j++)Check(!ScreenRect(rects[i]).Overlaps(ScreenRect(rects[j])),"Process Map no overlap "+names[i]+" / "+names[j]);
        }
    }
    private IEnumerator ValidateTutorialMouse(TutorialRuntime tour)
    {
        Check(!Field<Image>(tour,"blockerImage").raycastTarget,"Camera step releases spotlight input blocker");
        Vector2 point=new Vector2(Screen.width*.4f,Screen.height*.58f);
        var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count==0,"Camera tutorial spotlight has no UI raycast obstruction");
        var camera=FindAnyObjectByType<OrbitCameraController>();
        var priorFocusBehavior=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var mouse=InputSystem.AddDevice<Mouse>(); var keyboard=InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
        float azimuth=Field<float>(camera,"_azimuth");
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return new WaitForSecondsRealtime(1.5f);
        azimuth=Field<float>(camera,"_azimuth");
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point+new Vector2(40,10)}.WithButton(MouseButton.Left));yield return null;yield return null;
        Check(Mathf.Abs(Field<float>(camera,"_azimuth")-azimuth)>.01f,"Camera tutorial synthetic mouse drag rotates");
        Vector3 target=Field<Vector3>(camera,"_currentTarget");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point+new Vector2(70,20)}.WithButton(MouseButton.Left));yield return null;yield return null;
        Check(Vector3.Distance(target,Field<Vector3>(camera,"_currentTarget"))>.001f,"Camera tutorial synthetic Shift-drag pans");
        float zoom=camera.useOrthographic ? camera.orthographicSize : camera.distance;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point,scroll=new Vector2(0,1)});yield return null;yield return null;
        Check(Mathf.Abs((camera.useOrthographic ? camera.orthographicSize : camera.distance)-zoom)>.001f,"Camera tutorial synthetic wheel zooms");
        InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior=priorFocusBehavior;camera.FocusOverview();
    }
}
