using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashboardUI : MonoBehaviour
{
    public TMP_FontAsset interFont;

    Color BERRY   = new Color(0.322f, 0.043f, 0.133f);
    Color NAVY    = new Color(0.039f, 0.208f, 0.569f);
    Color SURFACE = new Color(0.953f, 0.961f, 0.976f);
    Color SCARD   = new Color(0.937f, 0.945f, 0.965f);
    Color BORDER  = new Color(0.863f, 0.882f, 0.922f);
    Color TPRI    = new Color(0.102f, 0.137f, 0.251f);
    Color TSEC    = new Color(0.353f, 0.392f, 0.502f);
    Color TMUT    = new Color(0.510f, 0.549f, 0.667f);
    Color GREEN   = new Color(0.071f, 0.490f, 0.259f);
    Color GSTART  = new Color(0.059f, 0.612f, 0.290f);
    Color GBG     = new Color(0.882f, 0.957f, 0.914f);
    Color GTXT    = new Color(0.047f, 0.333f, 0.169f);
    Color ABLUE   = new Color(0.255f, 0.565f, 0.973f);
    Color NTXT    = new Color(0.180f, 0.341f, 0.698f);
    Color GRID    = new Color(0.776f, 0.800f, 0.847f, 0.30f);
    Color SH2     = new Color(0.145f, 0.388f, 0.922f);
    Color SCO2    = new Color(0.863f, 0.149f, 0.149f);
    Color SMEOH   = new Color(0.086f, 0.639f, 0.259f);
    Color SREC    = new Color(0.576f, 0.200f, 0.922f);
    Color SSTM    = new Color(0.914f, 0.345f, 0.000f);
    Color SCW     = new Color(0.035f, 0.569f, 0.710f);
    Color ATEMP   = new Color(0.914f, 0.345f, 0.000f);
    Color APRES   = new Color(0.145f, 0.388f, 0.922f);
    Color ARAT    = new Color(0.576f, 0.200f, 0.922f);
    Color AGHSV   = new Color(0.035f, 0.569f, 0.710f);

    Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

    const float RW = 1440f, RH = 1024f;
    const float TB = 59f, SW = 240f, PW = 268f, MW = 932f;

    List<GameObject> navItems = new List<GameObject>();

    string[] navLabels = {
        "Dashboard", "Analytics",
        "Electrolyzer", "CO2 Absorber", "Desorber",
        "Compressor", "Reactor", "Heat Exchanger",
        "Flash Separator", "Distillation", "Storage Tanks"
    };

    void Awake()
    {
        Build();
    }

    public void Build()
    {
        var cgo = new GameObject("DashCanvas");
        var cv = cgo.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 20;
        var cs = cgo.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(RW, RH);
        cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight = 1f;
        cgo.AddComponent<GraphicRaycaster>();

        var root = MakePanel("Root", cgo.transform, RW, RH, 0, 0, Color.clear);
        var rrt = root.GetComponent<RectTransform>();
        rrt.anchorMin = Vector2.zero;
        rrt.anchorMax = Vector2.one;
        rrt.offsetMin = Vector2.zero;
        rrt.offsetMax = Vector2.zero;

        BuildTopbar(root.transform);
        BuildSidebar(root.transform);
        BuildMain(root.transform);
        BuildRightPanel(root.transform);
    }

    void BuildTopbar(Transform p)
    {
        MakePanel("Stripe", p, RW, 3, 0, -0, ABLUE);
        var bar = MakePanel("Topbar", p, RW, 56, 0, -3, BERRY);
        var logo = MakePanel("Logo", bar.transform, 32, 32, 18, -12, A(Color.white, 0.16f));
        MakeText("P", logo.transform, 13, Color.white, FontStyles.Bold, 11, -7);
        MakeText("Power-to-Methanol Digital Twin", bar.transform, 15, Color.white, FontStyles.Bold, 60, -9);
        MakeText("Group 8 - OVGU - Sprint 5", bar.transform, 11, A(Color.white, 0.6f), FontStyles.Normal, 61, -28);
        var chip = MakePanel("Chip", bar.transform, 182, 26, 866, -15, A(Color.white, 0.12f));
        MakePanel("ChipDot", chip.transform, 6, 6, 10, -10, new Color(0.133f, 0.773f, 0.369f));
        MakeText("Simulation running", chip.transform, 11, A(Color.white, 0.9f), FontStyles.Normal, 22, -6);
        MakePanel("BellBtn",     bar.transform, 32, 32, 1310, -11, A(Color.white, 0.13f));
        MakePanel("SettingsBtn", bar.transform, 32, 32, 1352, -11, A(Color.white, 0.13f));
        var av = MakePanel("Avatar", bar.transform, 34, 34, 1393, -11, A(Color.white, 0.22f));
        MakeText("CK", av.transform, 12, Color.white, FontStyles.Bold, 8, -9);
    }

    void BuildSidebar(Transform p)
    {
        var sb = MakePanel("Sidebar", p, SW, RH - TB, 0, -TB, NAVY);
        var rt = sb.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(SW, -TB);
        rt.anchoredPosition = new Vector2(0, 0);

        float sy = -22f;
        MakeSidebarLabel("OVERVIEW", sb.transform, sy); sy -= 22f;
        MakeNavItem("Dashboard", sb.transform, sy, false, 0); sy -= 40f;
        MakeNavItem("Analytics", sb.transform, sy, false, 1); sy -= 40f;
        sy -= 12f;
        MakeSidebarLabel("PLANT MODULES", sb.transform, sy); sy -= 22f;
        MakeNavItem("Electrolyzer",    sb.transform, sy, true,  2); sy -= 40f;
        MakeNavItem("CO2 Absorber",    sb.transform, sy, false, 3); sy -= 40f;
        MakeNavItem("Desorber",        sb.transform, sy, false, 4); sy -= 40f;
        MakeNavItem("Compressor",      sb.transform, sy, false, 5); sy -= 40f;
        MakeNavItem("Reactor",         sb.transform, sy, false, 6); sy -= 40f;
        MakeNavItem("Heat Exchanger",  sb.transform, sy, false, 7); sy -= 40f;
        MakeNavItem("Flash Separator", sb.transform, sy, false, 8); sy -= 40f;
        MakeNavItem("Distillation",    sb.transform, sy, false, 9); sy -= 40f;
        MakeNavItem("Storage Tanks",   sb.transform, sy, false, 10); sy -= 40f;

        MakePanel("Div", sb.transform, SW - 36, 1, 18, -872, A(Color.white, 0.15f));
        MakePanel("StatusDot", sb.transform, 7, 7, 18, -890, new Color(0.133f, 0.773f, 0.369f));
        MakeText("All systems normal", sb.transform, 12, A(Color.white, 0.7f), FontStyles.Normal, 32, -886);
        MakeText("Information", sb.transform, 13, A(Color.white, 0.8f), FontStyles.Normal, 20, -926);
    }

    void MakeSidebarLabel(string text, Transform parent, float y)
    {
        var t = MakeText(text, parent, 10, A(Color.white, 0.5f), FontStyles.Bold, 20, y, 200);
        t.characterSpacing = 1.4f;
    }

    void MakeNavItem(string label, Transform parent, float y, bool active, int idx)
    {
        var item = MakePanel("Nav_" + label, parent, SW, 40, 0, y, active ? A(Color.white, 0.13f) : Color.clear);
        if (active) MakePanel("Bar", item.transform, 4, 40, 0, 0, ABLUE);
        MakePanel("Dot", item.transform, 6, 6, active ? 21 : 18, -17, active ? ABLUE : A(Color.white, 0.4f));
        MakeText(label, item.transform, 13, active ? Color.white : A(Color.white, 0.88f), FontStyles.Normal, 35, -13);
        var btn = item.AddComponent<Button>();
        btn.targetGraphic = item.GetComponent<Image>();
        var cols = btn.colors;
        cols.normalColor = active ? A(Color.white, 0.13f) : Color.clear;
        cols.highlightedColor = A(Color.white, 0.20f);
        cols.pressedColor = A(Color.white, 0.08f);
        btn.colors = cols;
        int captured = idx;
        btn.onClick.AddListener(() => SelectNav(captured));
        navItems.Add(item);
    }

    void SelectNav(int idx)
    {
        for (int i = 0; i < navItems.Count; i++)
        {
            var item = navItems[i];
            bool isActive = i == idx;
            var img = item.GetComponent<Image>();
            if (img != null) img.color = isActive ? A(Color.white, 0.13f) : Color.clear;
            var bar = item.transform.Find("Bar");
            if (bar != null) bar.gameObject.SetActive(isActive);
            var dot = item.transform.Find("Dot");
            if (dot != null) dot.GetComponent<Image>().color = isActive ? ABLUE : A(Color.white, 0.4f);
            var lbl = item.transform.Find("Lbl");
            if (lbl != null)
            {
                var t = lbl.GetComponent<TextMeshProUGUI>();
                t.color = isActive ? Color.white : A(Color.white, 0.88f);
                t.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
            }
        }
        var titleObj = GameObject.Find("MainTitle");
        if (titleObj != null && idx < navLabels.Length)
            titleObj.GetComponent<TextMeshProUGUI>().text = "3D plant view - " + navLabels[idx];
    }

    void BuildMain(Transform p)
    {
        var main = MakePanel("Main", p, MW, RH - TB, SW, -TB, SURFACE);
        var mrt = main.GetComponent<RectTransform>();
        mrt.anchorMin = Vector2.zero;
        mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(SW, 0);
        mrt.offsetMax = new Vector2(-PW, -TB);

        var hdr = MakePanel("MainHeader", main.transform, MW, 52, 0, 0, Color.white);
        var hrt = hdr.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 1);
        hrt.anchorMax = new Vector2(1, 1);
        hrt.pivot = new Vector2(0, 1);
        hrt.sizeDelta = new Vector2(0, 52);
        hrt.anchoredPosition = Vector2.zero;
        AddBorderPanel(hdr, BORDER);

        var mt = MakeText("3D plant view - Electrolyzer", hdr.transform, 14, TPRI, FontStyles.Bold, 20, -18);
        mt.gameObject.name = "MainTitle";

        float tx = MW - 198f;
        string[] tlbls = { "3D view", "P&ID", "Data" };
        float[] tws = { 70, 56, 52 };
        for (int i = 0; i < 3; i++)
        {
            bool act = i == 0;
            var tab = MakePanel("Tab" + i, hdr.transform, tws[i], 28, tx, -12, act ? NAVY : Color.white);
            if (!act) AddBorderPanel(tab, BORDER);
            MakeText(tlbls[i], tab.transform, 12, act ? Color.white : TSEC, FontStyles.Normal, 10, -8);
            tx += tws[i] + 4f;
        }

        float vpH = RH - TB - 52 - 24;
        var vp = MakePanel("Viewport", main.transform, MW - 32, vpH, 16, -68, new Color(0.918f, 0.929f, 0.949f));
        AddBorderPanel(vp, BORDER);

        int gc = Mathf.FloorToInt((MW - 32) / 40);
        int gr = Mathf.FloorToInt(vpH / 40);
        for (int i = 1; i <= gr; i++) MakePanel("GH" + i, vp.transform, MW - 32, 1, 0, -(i * 40), GRID);
        for (int i = 1; i <= gc; i++) MakePanel("GV" + i, vp.transform, 1, vpH, i * 40, 0, GRID);

        var card = MakePanel("VPCard", vp.transform, 230, 80, (MW - 32) / 2 - 115, -(vpH / 2 - 40), Color.white);
        AddBorderPanel(card, BORDER);
        MakeText("Unity 3D Viewport",        card.transform, 15, TSEC, FontStyles.Bold,   20, -16);
        MakeText("Embedded via WebGL build",  card.transform, 11, TMUT, FontStyles.Normal, 30, -42);

        var fd = MakePanel("FocusDrop", vp.transform, 140, 28, MW - 32 - 148, -14, Color.white);
        AddBorderPanel(fd, BORDER);
        MakeText("Focus: Electrolyzer  v", fd.transform, 11, TSEC, FontStyles.Normal, 10, -7);

        float vcx = 14f;
        string[] vcl = { "Reset view", "Focus", "Grid", "Pipes" };
        float[] vcw = { 88, 62, 54, 58 };
        for (int i = 0; i < 4; i++)
        {
            var b = MakePanel("VC" + i, vp.transform, vcw[i], 28, vcx, -(vpH - 42), Color.white);
            AddBorderPanel(b, BORDER);
            MakeText(vcl[i], b.transform, 11, TSEC, FontStyles.Normal, 8, -7);
            vcx += vcw[i] + 6f;
        }
    }

    void BuildRightPanel(Transform p)
    {
        var rp = MakePanel("RightPanel", p, PW, RH - TB, RW - PW, -TB, Color.white);
        var rt = rp.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.sizeDelta = new Vector2(PW, -TB);
        rt.anchoredPosition = Vector2.zero;
        AddBorderPanel(rp, BORDER);

        float ry = 0f;
        float cw = (PW - 40f) / 2f;

        var live = MakeSection("Live Conditions",    rp.transform, PW, 132, ry); ry += 132f;
        MakeMetricCard(live.transform, "Temperature",  "250 C",      16,    38, cw, ATEMP);
        MakeMetricCard(live.transform, "Pressure",     "70 bar",     cw+24, 38, cw, APRES);
        MakeMetricCard(live.transform, "H2 CO2 ratio", "3.0 mol/mol",16,    84, cw, ARAT);
        MakeMetricCard(live.transform, "GHSV",         "8000 h-1",   cw+24, 84, cw, AGHSV);

        var op = MakeSection("Operating Targets", rp.transform, PW, 130, ry); ry += 130f;
        MakeProgressRow(op.transform, "Yield n",        72, GREEN,  38);
        MakeProgressRow(op.transform, "CO2 capture",    88, SH2,    60);
        MakeProgressRow(op.transform, "Electrolyzer n", 65, SREC,   82);
        MakeProgressRow(op.transform, "MeOH purity",    95, SCW,   104);

        var st = MakeSection("Key Streams", rp.transform, PW, 168, ry); ry += 168f;
        MakeStreamRow(st.transform, "Hydrogen H2",   "4.2 kg/h",  SH2,   38);
        MakeStreamRow(st.transform, "Carbon dioxide","18.7 kg/h", SCO2,  60);
        MakeStreamRow(st.transform, "Methanol",      "9.1 kg/h",  SMEOH, 82);
        MakeStreamRow(st.transform, "Recycle gas",   "3.3 kg/h",  SREC, 104);
        MakeStreamRow(st.transform, "Steam",         "6.8 kg/h",  SSTM, 126);
        MakeStreamRow(st.transform, "Cooling water", "42 kg/h",   SCW,  148);

        var al = MakeSection("Plant Alerts", rp.transform, PW, 108, ry); ry += 108f;
        var ab = MakePanel("AlertBanner", al.transform, PW - 32, 60, 16, -36, GBG);
        AddBorderPanel(ab, A(GREEN, 0.2f));
        MakePanel("GBar", ab.transform, 4, 60, 0, 0, GREEN);
        MakeText("All systems normal",           ab.transform, 13, GTXT,          FontStyles.Bold,   14, -8,  PW - 64);
        MakeText("No active alarms - 0:12 ago",  ab.transform, 11, A(GREEN, 0.8f),FontStyles.Normal, 14, -27, PW - 64);
        MakeText("View alert log ->",            ab.transform, 11, NTXT,          FontStyles.Normal, 14, -46);

        var qa = MakeSection("Quick Actions", rp.transform, PW, 116, ry); ry += 116f;
        float bw = (PW - 40f) / 2f;
        MakeActionButton(qa.transform, "Start",  GSTART,  Color.white, "Start",  16,      36, bw);
        MakeActionButton(qa.transform, "Pause",  SURFACE, TPRI,        "Pause",  16+bw+8, 36, bw);
        MakeActionButton(qa.transform, "Reset",  SURFACE, TPRI,        "Reset",  16,      82, bw);
        MakeActionButton(qa.transform, "Export", SURFACE, NTXT,        "Export", 16+bw+8, 82, bw);
    }

    GameObject MakeSection(string title, Transform parent, float w, float h, float ry)
    {
        var s = MakePanel("Sec_" + title, parent, w, h, 0, -ry, Color.white);
        MakePanel("Div", s.transform, w, 1, 0, -(h - 1), A(BORDER, 0.7f));
        var lbl = MakeText(title.ToUpper(), s.transform, 10, TMUT, FontStyles.Bold, 16, -13, w - 32);
        lbl.characterSpacing = 1.2f;
        MakePanel("TLine", s.transform, w - 32, 1, 16, -30, A(BORDER, 0.8f));
        return s;
    }

    void MakeMetricCard(Transform parent, string name, string val, float x, float y, float cw, Color accent)
    {
        var card = MakePanel("MC_" + name, parent, cw, 38, x, -y, SCARD);
        MakePanel("Bar", card.transform, 3, 38, 0, 0, accent);
        MakeText(name, card.transform, 9,  TMUT, FontStyles.Normal, 10, -4,  cw - 14);
        MakeText(val,  card.transform, 13, TPRI, FontStyles.Bold,   10, -18);
    }

    void MakeProgressRow(Transform parent, string label, float pct, Color col, float y)
    {
        MakeText(label, parent, 12, TSEC, FontStyles.Normal, 16, -y, 130);
        MakePanel("OBg",   parent, 70, 5, 150, -(y + 7), BORDER);
        MakePanel("OFill", parent, Mathf.Round(70 * pct / 100f), 5, 150, -(y + 7), col);
        MakeText(pct + "%", parent, 12, col, FontStyles.Bold, PW - 38, -y);
    }

    void MakeStreamRow(Transform parent, string name, string val, Color col, float y)
    {
        MakePanel("Dot", parent, 8, 8, 16, -(y + 6), col);
        MakeText(name, parent, 13, TPRI, FontStyles.Normal, 30,      -y);
        MakeText(val,  parent, 12, TSEC, FontStyles.Bold,   PW - 68, -y);
    }

    void MakeActionButton(Transform parent, string name, Color bg, Color tc, string label, float x, float y, float bw)
    {
        var btn = MakePanel("QA_" + name, parent, bw, 36, x, -y, bg);
        AddBorderPanel(btn, bg == SURFACE ? BORDER : A(GSTART, 0.25f));
        MakeText(label, btn.transform, 12, tc, FontStyles.Normal, 10, -10);
        var b = btn.AddComponent<Button>();
        b.targetGraphic = btn.GetComponent<Image>();
    }

    GameObject MakePanel(string name, Transform parent, float w, float h, float x, float y, Color fill)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
        var img = go.AddComponent<Image>();
        img.color = fill;
        img.raycastTarget = false;
        return go;
    }

    TextMeshProUGUI MakeText(string text, Transform parent, float size, Color color,
                             FontStyles style, float x, float y, float fixedW = 0)
    {
        var go = new GameObject("Lbl");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (interFont != null) tmp.font = interFont;
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.fontStyle = style;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (fixedW > 0)
        {
            rt.sizeDelta = new Vector2(fixedW, size * 1.5f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
        }
        else
        {
            rt.sizeDelta = new Vector2(400, size * 1.5f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }
        return tmp;
    }

    void AddBorderPanel(GameObject go, Color col)
    {
        var border = new GameObject("_border");
        border.transform.SetParent(go.transform, false);
        border.transform.SetAsFirstSibling();
        var rt = border.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-1, -1);
        rt.offsetMax = new Vector2(1, 1);
        var img = border.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
    }
}