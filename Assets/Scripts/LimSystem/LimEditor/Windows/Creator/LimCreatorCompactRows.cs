using System.Collections.Generic;
using UnityEngine;

public partial class LimCreatorManager
{
    private const float CreatorRowHeight = 30, CreatorSectionGap = 5;

    /// <summary>
    /// Lays the Creator's buttons out again once every runtime row is in:
    /// rows touching, as the scene's first eight always were, with a small
    /// gap only after Create Scroll Speed (creating things above, working on
    /// the selection below) and after the last row, before the tools.
    ///
    /// Each row added at runtime pushed its neighbours down by 35, a row and
    /// a gap, so every one of them brought a gap of its own; the scene also
    /// had one above Delete Selected. Rows side by side (Flip, Select) share
    /// a height and move together; the fields on a row are its children and
    /// move with it.
    /// </summary>
    private void CompactCreatorRows()
    {
        if (CreateScrollSpeedText == null || ClickToCreateManager == null) return;
        RectTransform ScrollRow = CreateScrollSpeedText.rectTransform.parent as RectTransform;
        if (ScrollRow == null || ScrollRow.parent == null) return;
        Transform Content = ScrollRow.parent;

        // Rows are the top-anchored children with a height; the tools below
        // them are zero-height rects placed by ArrangeCreatorsUi.
        SortedDictionary<float, List<RectTransform>> Rows = new SortedDictionary<float, List<RectTransform>>();
        for (int i = 0; i < Content.childCount; ++i)
        {
            RectTransform Child = Content.GetChild(i) as RectTransform;
            if (Child == null || Child.anchorMin.y != 1 || Child.sizeDelta.y < CreatorRowHeight - 1) continue;
            float Key = -Mathf.Round(Child.anchoredPosition.y * 2) / 2;
            List<RectTransform> Row;
            if (!Rows.TryGetValue(Key, out Row)) Rows[Key] = Row = new List<RectTransform>();
            Row.Add(Child);
        }
        if (Rows.Count == 0) return;

        float Y = 0;
        foreach (List<RectTransform> Row in Rows.Values)
        {
            bool AfterScroll = Row.Contains(ScrollRow);
            foreach (RectTransform Rect in Row) Rect.anchoredPosition = new Vector2(Rect.anchoredPosition.x, Y);
            Y -= CreatorRowHeight;
            if (AfterScroll) Y -= CreatorSectionGap;
        }
        CreatorHeaderHeight = -Y + CreatorSectionGap;
        if (ClickToCreateManager.ToolBase != null)
        {
            RectTransform Tool = ClickToCreateManager.ToolBase.ToolRect;
            Tool.anchoredPosition = new Vector2(Tool.anchoredPosition.x, -CreatorHeaderHeight);
        }
    }
}
