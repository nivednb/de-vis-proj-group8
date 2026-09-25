using UnityEngine;
using UnityEngine.UI;
using Icon = UITheme.Icon;
using W = UITheme.Weight;

/// <summary>
/// Hover card for the methanol reactor: the reaction the simulation models, with live rates
/// from the recycle mass balance, plus the side reactions a real Cu/ZnO/Al₂O₃ bed also runs.
/// Shown beside the reactor's hover pill; it never takes pointer input, so the pill underneath
/// stays clickable.
/// </summary>
public sealed class ReactorReactionCard : MonoBehaviour
{
    const float Width = 360f;
    const float Pad = 18f;
    const float Inner = Width - 2f * Pad;
    const float Gap = 14f;
    const float FadeSpeed = 10f;

    // Molar masses (kg/kmol) and standard reaction enthalpy, matching RecycleMassBalanceEngine.
    const float MMeOH = 32.04186f;
    const float MCo2 = 44.00950f;
    const float MainReactionEnthalpyKJPerMol = 49.5f;

    RectTransform card;
    CanvasGroup group;
    Canvas canvas;
    Text conditions, conversion, co2Used, methanolMade, heatReleased;
    float target;

    public static ReactorReactionCard Create(Canvas canvas, Color accent)
    {
        RectTransform root = UITheme.Card("Reactor Reaction Card", canvas.transform, 16f, UITheme.Glass, 30f, 10f, 0.24f);
        ReactorReactionCard c = root.gameObject.AddComponent<ReactorReactionCard>();
        c.canvas = canvas;
        c.card = root;
        c.Build(accent);
        return c;
    }

    void Build(Color accent)
    {
        card.anchorMin = card.anchorMax = Vector2.zero;
        card.pivot = new Vector2(0f, .5f);
        group = card.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
        foreach (Graphic g in card.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        float y = Pad;
        Image badge = UITheme.Badge("Badge", card, Icon.Flask, accent, 34f, 10f, 18f);
        UITheme.TopLeft(badge.rectTransform, Pad, y, 34f, 34f);
        Text title = UITheme.Label("Title", card, "Reactions in the catalyst bed", 15f, W.ExtraBold, UITheme.Ink);
        UITheme.TopLeft(title.rectTransform, Pad + 46f, y - 1f, Inner - 46f, 20f);
        conditions = UITheme.Label("Conditions", card, "", 11.5f, W.SemiBold, UITheme.Subtle);
        UITheme.TopLeft(conditions.rectTransform, Pad + 46f, y + 19f, Inner - 46f, 16f);
        y += 34f + 16f;

        // The reaction this simulation actually solves.
        y = SectionLabel("Modelled in this simulation", y, accent);
        RectTransform main = UITheme.Panel("Main Reaction", card, UITheme.WithAlpha(accent, .07f), 12f).rectTransform;
        UITheme.Border(main, UITheme.WithAlpha(accent, .25f), 12f);
        float mainTop = y;
        float my = 12f;
        Equation(main, "CO₂ + 3 H₂", "CH₃OH + H₂O", 12f, my, 17f, UITheme.Ink);
        my += 28f;
        Text mainNote = UITheme.Label("Main Note", main,
            "CO₂ hydrogenation to methanol\nExothermic, ΔH° = −49.5 kJ/mol  ·  4 mol of gas become 2",
            11.5f, W.SemiBold, UITheme.Muted, TextAnchor.UpperLeft, true);
        UITheme.TopLeft(mainNote.rectTransform, 12f, my, Inner - 24f, 32f);
        float noteH = Mathf.Ceil(mainNote.preferredHeight) + 2f;
        mainNote.rectTransform.sizeDelta = new Vector2(Inner - 24f, noteH);
        my += noteH + 10f;

        float cw = (Inner - 24f - 3f * 6f) / 4f;
        conversion = Stat(main, "Conversion", 12f, my, cw);
        co2Used = Stat(main, "CO₂ reacted", 12f + (cw + 6f), my, cw);
        methanolMade = Stat(main, "CH₃OH formed", 12f + 2f * (cw + 6f), my, cw);
        heatReleased = Stat(main, "Heat released", 12f + 3f * (cw + 6f), my, cw);
        my += 46f + 12f;
        UITheme.TopLeft(main, Pad, mainTop, Inner, my);
        y += my + 16f;

        // Side reactions: real, but deliberately outside this educational model.
        y = SectionLabel("Also occur in real reactors  ·  not modelled", y, UITheme.Subtle);
        y = SideReaction("Reverse water-gas shift", "CO₂ + H₂", "CO + H₂O",
            "Endothermic, ΔH° = +41.2 kJ/mol  ·  favoured at high temperature", y);
        y = SideReaction("CO hydrogenation", "CO + 2 H₂", "CH₃OH",
            "Exothermic, ΔH° = −90.6 kJ/mol  ·  turns that CO into methanol", y);

        Text why = UITheme.Label("Why", card,
            "Both methanol reactions release heat and lose moles, so a hotter bed reacts faster but the equilibrium shifts back to the reactants, while higher pressure pushes it toward methanol.",
            11f, W.Medium, UITheme.Subtle, TextAnchor.UpperLeft, true);
        why.lineSpacing = 1.1f;
        UITheme.TopLeft(why.rectTransform, Pad, y + 2f, Inner, 40f);
        float whyH = Mathf.Ceil(why.preferredHeight) + 2f;
        why.rectTransform.sizeDelta = new Vector2(Inner, whyH);
        y += whyH + Pad;

        card.sizeDelta = new Vector2(Width, y);
        card.gameObject.SetActive(false);
    }

    float SectionLabel(string text, float y, Color color)
    {
        Text label = UITheme.Label("Section", card, text.ToUpperInvariant(), 10.5f, W.ExtraBold, color);
        UITheme.TopLeft(label.rectTransform, Pad, y, Inner, 14f);
        return y + 20f;
    }

    float SideReaction(string name, string left, string right, string note, float y)
    {
        Equation(card, left, right, Pad, y, 14f, UITheme.Ink2);
        Text caption = UITheme.Label(name, card, name, 11f, W.Bold, UITheme.Subtle, TextAnchor.MiddleRight);
        UITheme.TopLeft(caption.rectTransform, Pad, y, Inner, 20f);
        Text detail = UITheme.Label(name + " Note", card, note, 11f, W.Medium, UITheme.Subtle);
        UITheme.TopLeft(detail.rectTransform, Pad, y + 21f, Inner, 15f);
        return y + 44f;
    }

    /// <summary>Lays out "left ⇌ right". The UI font has no ⇌ glyph, so the equilibrium
    /// arrow is drawn as a → stacked over a ←.</summary>
    static void Equation(Transform parent, string left, string right, float x, float y, float size, Color color)
    {
        Text l = UITheme.Label("Reactants", parent, left, size, W.ExtraBold, color);
        float lw = Mathf.Ceil(l.preferredWidth);
        UITheme.TopLeft(l.rectTransform, x, y, lw + 2f, size + 6f);

        float ax = x + lw + 8f;
        float arrowSize = size * .8f;
        Text forward = UITheme.Label("Forward", parent, "→", arrowSize, W.Bold, color, TextAnchor.MiddleCenter);
        UITheme.TopLeft(forward.rectTransform, ax, y - size * .18f, 20f, size);
        Text backward = UITheme.Label("Backward", parent, "←", arrowSize, W.Bold, color, TextAnchor.MiddleCenter);
        UITheme.TopLeft(backward.rectTransform, ax, y + size * .32f, 20f, size);

        Text r = UITheme.Label("Products", parent, right, size, W.ExtraBold, color);
        UITheme.TopLeft(r.rectTransform, ax + 28f, y, Mathf.Ceil(r.preferredWidth) + 2f, size + 6f);
    }

    static Text Stat(Transform parent, string caption, float x, float y, float width)
    {
        Image tile = UITheme.Panel(caption, parent, Color.white, 9f);
        UITheme.TopLeft(tile.rectTransform, x, y, width, 46f);
        UITheme.Border(tile.rectTransform, UITheme.Line, 9f);
        Text cap = UITheme.Label("Caption", tile.transform, caption, 10f, W.Bold, UITheme.Subtle);
        UITheme.TopLeft(cap.rectTransform, 8f, 6f, width - 12f, 14f);
        Text value = UITheme.Label("Value", tile.transform, "—", 13.5f, W.ExtraBold, UITheme.Ink);
        UITheme.TopLeft(value.rectTransform, 8f, 22f, width - 12f, 18f);
        return value;
    }

    /// <summary>Called every frame by the module panel runtime with the reactor pill's rect,
    /// or null when the reactor is not hovered.</summary>
    public void Track(RectTransform pill)
    {
        target = pill != null ? 1f : 0f;
        if (pill != null)
        {
            if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);
            Place(pill);
            Refresh();
        }

        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * FadeSpeed);
        if (group.alpha <= 0f && target <= 0f && card.gameObject.activeSelf) card.gameObject.SetActive(false);
    }

    void Place(RectTransform pill)
    {
        Vector3[] corners = new Vector3[4];
        pill.GetWorldCorners(corners);
        float scale = canvas.scaleFactor;
        Vector2 size = card.sizeDelta * scale;
        float gap = Gap * scale;
        float midY = (corners[0].y + corners[1].y) * .5f;

        // Right of the pill unless that would run off screen; then to its left.
        bool left = corners[2].x + gap + size.x > Screen.width - 8f * scale;
        float x = left ? corners[0].x - gap - size.x : corners[2].x + gap;
        float y = Mathf.Clamp(midY, size.y * .5f + 8f * scale, Screen.height - size.y * .5f - 8f * scale);
        card.pivot = new Vector2(0f, .5f);
        card.position = new Vector3(Mathf.Max(8f * scale, x), y, 0f);
    }

    void Refresh()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim == null) return;
        PlantProcessSimulator.ProcessSnapshot s = sim.Current;
        conditions.text = $"Cu/ZnO/Al₂O₃ catalyst  ·  {s.reactorTemperatureC:F0} °C  ·  {s.reactorPressureBar:F0} bar";
        conversion.text = $"{s.reactorYieldPercent:F1} %";

        RecycleMassBalanceEngine balance = sim.MassBalance;
        float methanol = balance != null ? balance.methanolProductKgHr : 0f;
        float extentKmolH = methanol / MMeOH;
        co2Used.text = $"{extentKmolH * MCo2:F0} kg/h";
        methanolMade.text = $"{methanol:F0} kg/h";
        heatReleased.text = $"{extentKmolH * MainReactionEnthalpyKJPerMol * 1000f / 3600f:F0} kW";
    }
}
