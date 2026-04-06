using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace ThemeLevelDesigner.Editor
{
    /// <summary>
    /// The main map canvas where sections are displayed and can be placed via drag-and-drop.
    /// Supports pan (MMB), zoom (scroll), selection (LMB), and context menu (RMB).
    /// </summary>
    public class MapCanvasElement : VisualElement
    {
        readonly LevelDesignerWindow _window;

        // View state
        Vector2 _panOffset = Vector2.zero;
        float _zoom = 1f;
        const float MinZoom = 0.2f;
        const float MaxZoom = 4f;
        const float CellScreenSize = 32f; // base pixels per grid cell

        // Interaction
        bool _isPanning;
        Vector2 _panStart;
        PlacedSection _hoveredSection;
        PlacedSection _selectedSection;

        // Drag preview
        bool _isDragHovering;
        Vector2Int _dragPreviewPos;
        Vector2Int _dragPreviewSize;
        bool _dragCanPlace;

        // Multi-select
        bool _isBoxSelecting;
        Vector2 _boxStart, _boxEnd;
        readonly List<PlacedSection> _multiSelected = new();

        public MapCanvasElement(LevelDesignerWindow window)
        {
            _window = window;

            focusable = true;
            style.overflow = Overflow.Hidden;

            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<WheelEvent>(OnScroll);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<MouseLeaveEvent>(evt => { _isPanning = false; _isDragHovering = false; });
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            // Drag-and-drop from palette
            RegisterCallback<DragEnterEvent>(OnDragEnter);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DragLeaveEvent>(OnDragLeave);
        }

        // ==================== COORDINATE CONVERSION ====================

        Vector2 GridToScreen(Vector2Int gridPos)
        {
            float cellPx = CellScreenSize * _zoom;
            var center = contentRect.center;
            return new Vector2(
                center.x + (gridPos.x * cellPx) + _panOffset.x,
                center.y + (gridPos.y * cellPx) + _panOffset.y
            );
        }

        Vector2Int ScreenToGrid(Vector2 screenPos)
        {
            float cellPx = CellScreenSize * _zoom;
            var center = contentRect.center;
            float gx = (screenPos.x - center.x - _panOffset.x) / cellPx;
            float gy = (screenPos.y - center.y - _panOffset.y) / cellPx;
            return new Vector2Int(Mathf.FloorToInt(gx), Mathf.FloorToInt(gy));
        }

        // ==================== RENDERING ====================

        void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            var painter = ctx.painter2D;
            var rect = contentRect;

            float cellPx = CellScreenSize * _zoom;

            // Grid
            DrawGrid(painter, rect, cellPx);

            // Placed sections
            var map = _window.CurrentMap;
            if (map != null)
            {
                foreach (var placed in map.placedSections)
                    DrawPlacedSection(painter, placed, cellPx);
            }

            // Drag preview
            if (_isDragHovering)
                DrawDragPreview(painter, cellPx);

            // Box select
            if (_isBoxSelecting)
                DrawBoxSelect(painter);

            // Origin marker
            var origin = GridToScreen(Vector2Int.zero);
            painter.fillColor = new Color(1, 1, 1, 0.3f);
            painter.BeginPath();
            painter.Arc(origin, 4 * _zoom, 0, 360);
            painter.Fill();
        }

        void DrawGrid(Painter2D painter, Rect rect, float cellPx)
        {
            // Calculate visible grid range
            var topLeft = ScreenToGrid(Vector2.zero);
            var bottomRight = ScreenToGrid(new Vector2(rect.width, rect.height));

            painter.strokeColor = new Color(1, 1, 1, 0.06f);
            painter.lineWidth = 1;

            for (int x = topLeft.x - 1; x <= bottomRight.x + 1; x++)
            {
                var start = GridToScreen(new Vector2Int(x, topLeft.y - 1));
                var end = GridToScreen(new Vector2Int(x, bottomRight.y + 2));
                painter.BeginPath();
                painter.MoveTo(start);
                painter.LineTo(end);
                painter.Stroke();
            }

            for (int y = topLeft.y - 1; y <= bottomRight.y + 1; y++)
            {
                var start = GridToScreen(new Vector2Int(topLeft.x - 1, y));
                var end = GridToScreen(new Vector2Int(bottomRight.x + 2, y));
                painter.BeginPath();
                painter.MoveTo(start);
                painter.LineTo(end);
                painter.Stroke();
            }

            // Axis lines
            painter.strokeColor = new Color(1, 1, 1, 0.15f);
            painter.lineWidth = 1.5f;

            var xAxisStart = GridToScreen(new Vector2Int(topLeft.x - 1, 0));
            var xAxisEnd = GridToScreen(new Vector2Int(bottomRight.x + 2, 0));
            painter.BeginPath();
            painter.MoveTo(xAxisStart);
            painter.LineTo(xAxisEnd);
            painter.Stroke();

            var yAxisStart = GridToScreen(new Vector2Int(0, topLeft.y - 1));
            var yAxisEnd = GridToScreen(new Vector2Int(0, bottomRight.y + 2));
            painter.BeginPath();
            painter.MoveTo(yAxisStart);
            painter.LineTo(yAxisEnd);
            painter.Stroke();
        }

        void DrawPlacedSection(Painter2D painter, PlacedSection placed, float cellPx)
        {
            var screenPos = GridToScreen(placed.gridPos);
            var size = placed.RotatedSize;
            var rectSize = new Vector2(size.x * cellPx, size.y * cellPx);

            // Fill
            bool isSelected = placed == _selectedSection || _multiSelected.Contains(placed);
            bool isHovered = placed == _hoveredSection;

            Color fillColor;
            if (placed.sourceTheme != null)
            {
                fillColor = placed.sourceTheme.themeColor;
                fillColor.a = isSelected ? 0.45f : isHovered ? 0.35f : 0.25f;
            }
            else
            {
                fillColor = new Color(0.4f, 0.4f, 0.4f, 0.25f);
            }

            painter.fillColor = fillColor;
            painter.BeginPath();
            painter.MoveTo(screenPos);
            painter.LineTo(screenPos + new Vector2(rectSize.x, 0));
            painter.LineTo(screenPos + rectSize);
            painter.LineTo(screenPos + new Vector2(0, rectSize.y));
            painter.ClosePath();
            painter.Fill();

            // Room group color tint
            if (!string.IsNullOrEmpty(placed.roomGroupId))
            {
                var map = _window.CurrentMap;
                var group = map?.roomGroups.Find(g => g.groupId == placed.roomGroupId);
                if (group != null)
                {
                    var rc = group.roomColor;
                    rc.a = 0.12f;
                    painter.fillColor = rc;
                    painter.BeginPath();
                    painter.MoveTo(screenPos);
                    painter.LineTo(screenPos + new Vector2(rectSize.x, 0));
                    painter.LineTo(screenPos + rectSize);
                    painter.LineTo(screenPos + new Vector2(0, rectSize.y));
                    painter.ClosePath();
                    painter.Fill();
                }
            }

            // Border
            Color borderColor = isSelected ? Color.white :
                (placed.sourceTheme != null ? placed.sourceTheme.themeColor : Color.gray);
            painter.strokeColor = borderColor;
            painter.lineWidth = isSelected ? 2.5f : 1.5f;
            painter.BeginPath();
            painter.MoveTo(screenPos);
            painter.LineTo(screenPos + new Vector2(rectSize.x, 0));
            painter.LineTo(screenPos + rectSize);
            painter.LineTo(screenPos + new Vector2(0, rectSize.y));
            painter.ClosePath();
            painter.Stroke();
        }

        void DrawDragPreview(Painter2D painter, float cellPx)
        {
            var screenPos = GridToScreen(_dragPreviewPos);
            var rectSize = new Vector2(_dragPreviewSize.x * cellPx, _dragPreviewSize.y * cellPx);

            Color fill = _dragCanPlace
                ? new Color(0.2f, 0.8f, 0.3f, 0.3f)
                : new Color(0.8f, 0.2f, 0.2f, 0.3f);
            Color stroke = _dragCanPlace
                ? new Color(0.3f, 1f, 0.4f, 0.8f)
                : new Color(1f, 0.3f, 0.3f, 0.8f);

            painter.fillColor = fill;
            painter.BeginPath();
            painter.MoveTo(screenPos);
            painter.LineTo(screenPos + new Vector2(rectSize.x, 0));
            painter.LineTo(screenPos + rectSize);
            painter.LineTo(screenPos + new Vector2(0, rectSize.y));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = stroke;
            painter.lineWidth = 2;
            painter.BeginPath();
            painter.MoveTo(screenPos);
            painter.LineTo(screenPos + new Vector2(rectSize.x, 0));
            painter.LineTo(screenPos + rectSize);
            painter.LineTo(screenPos + new Vector2(0, rectSize.y));
            painter.ClosePath();
            painter.Stroke();
        }

        void DrawBoxSelect(Painter2D painter)
        {
            var min = Vector2.Min(_boxStart, _boxEnd);
            var max = Vector2.Max(_boxStart, _boxEnd);
            var size = max - min;

            painter.fillColor = new Color(0.3f, 0.5f, 1f, 0.15f);
            painter.BeginPath();
            painter.MoveTo(min);
            painter.LineTo(min + new Vector2(size.x, 0));
            painter.LineTo(max);
            painter.LineTo(min + new Vector2(0, size.y));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = new Color(0.4f, 0.6f, 1f, 0.8f);
            painter.lineWidth = 1;
            painter.BeginPath();
            painter.MoveTo(min);
            painter.LineTo(min + new Vector2(size.x, 0));
            painter.LineTo(max);
            painter.LineTo(min + new Vector2(0, size.y));
            painter.ClosePath();
            painter.Stroke();
        }

        // ==================== INPUT ====================

        void OnScroll(WheelEvent evt)
        {
            float delta = -evt.delta.y * 0.08f;
            float prevZoom = _zoom;
            _zoom = Mathf.Clamp(_zoom + delta, MinZoom, MaxZoom);

            // Zoom toward mouse
            float factor = _zoom / prevZoom;
            var mouseLocal = evt.localMousePosition - contentRect.center;
            _panOffset = (mouseLocal + _panOffset) * factor - mouseLocal;

            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        void OnMouseDown(MouseDownEvent evt)
        {
            Focus();

            // Middle mouse: pan
            if (evt.button == 2)
            {
                _isPanning = true;
                _panStart = evt.localMousePosition - _panOffset;
                evt.StopPropagation();
                return;
            }

            // Left click: select or box select
            if (evt.button == 0)
            {
                var gridPos = ScreenToGrid(evt.localMousePosition);
                var map = _window.CurrentMap;
                var hit = map?.GetAt(gridPos);

                if (hit != null)
                {
                    _selectedSection = hit;
                    _window.OnSectionSelected(hit);
                    MarkDirtyRepaint();
                }
                else
                {
                    // Start box select
                    _isBoxSelecting = true;
                    _boxStart = _boxEnd = evt.localMousePosition;
                    _selectedSection = null;
                    _multiSelected.Clear();
                    _window.OnSectionDeselected();
                    MarkDirtyRepaint();
                }
                evt.StopPropagation();
            }

            // Right click: context menu
            if (evt.button == 1)
            {
                var gridPos = ScreenToGrid(evt.localMousePosition);
                var map = _window.CurrentMap;
                var hit = map?.GetAt(gridPos);
                ShowContextMenu(hit);
                evt.StopPropagation();
            }
        }

        void OnMouseMove(MouseMoveEvent evt)
        {
            if (_isPanning)
            {
                _panOffset = evt.localMousePosition - _panStart;
                MarkDirtyRepaint();
                return;
            }

            if (_isBoxSelecting)
            {
                _boxEnd = evt.localMousePosition;
                MarkDirtyRepaint();
                return;
            }

            // Hover detection
            var gridPos = ScreenToGrid(evt.localMousePosition);
            var map = _window.CurrentMap;
            var newHover = map?.GetAt(gridPos);
            if (newHover != _hoveredSection)
            {
                _hoveredSection = newHover;
                MarkDirtyRepaint();
            }
        }

        void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button == 2)
            {
                _isPanning = false;
                evt.StopPropagation();
            }

            if (evt.button == 0 && _isBoxSelecting)
            {
                _isBoxSelecting = false;
                FinalizeBoxSelect();
                MarkDirtyRepaint();
                evt.StopPropagation();
            }
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                DeleteSelected();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.R && _selectedSection != null)
            {
                RotateSelected();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.F)
            {
                // Focus / reset view
                _panOffset = Vector2.zero;
                _zoom = 1f;
                MarkDirtyRepaint();
                evt.StopPropagation();
            }
        }

        // ==================== DRAG & DROP ====================

        void OnDragEnter(DragEnterEvent evt)
        {
            var entry = DragAndDrop.GetGenericData("SectionEntry") as SectionEntry;
            if (entry != null)
            {
                _isDragHovering = true;
                _dragPreviewSize = entry.gridSize;
            }
        }

        void OnDragUpdated(DragUpdatedEvent evt)
        {
            var entry = DragAndDrop.GetGenericData("SectionEntry") as SectionEntry;
            if (entry == null) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            _dragPreviewPos = ScreenToGrid(evt.localMousePosition);
            _dragPreviewSize = entry.gridSize;

            var map = _window.CurrentMap;
            _dragCanPlace = map == null || map.CanPlace(_dragPreviewPos, entry.gridSize);

            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        void OnDragPerform(DragPerformEvent evt)
        {
            var entry = DragAndDrop.GetGenericData("SectionEntry") as SectionEntry;
            if (entry == null) return;

            DragAndDrop.AcceptDrag();
            var gridPos = ScreenToGrid(evt.localMousePosition);
            _window.OnSectionPlaced(gridPos, entry);

            _isDragHovering = false;
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        void OnDragLeave(DragLeaveEvent evt)
        {
            _isDragHovering = false;
            MarkDirtyRepaint();
        }

        // ==================== ACTIONS ====================

        void FinalizeBoxSelect()
        {
            var map = _window.CurrentMap;
            if (map == null) return;

            _multiSelected.Clear();
            var min = Vector2.Min(_boxStart, _boxEnd);
            var max = Vector2.Max(_boxStart, _boxEnd);

            foreach (var placed in map.placedSections)
            {
                var screenPos = GridToScreen(placed.gridPos);
                var size = placed.RotatedSize;
                float cellPx = CellScreenSize * _zoom;
                var endPos = screenPos + new Vector2(size.x * cellPx, size.y * cellPx);

                // Check overlap
                if (screenPos.x < max.x && endPos.x > min.x &&
                    screenPos.y < max.y && endPos.y > min.y)
                {
                    _multiSelected.Add(placed);
                }
            }
        }

        void DeleteSelected()
        {
            var map = _window.CurrentMap;
            if (map == null) return;

            Undo.RecordObject(map, "Delete Section(s)");

            if (_multiSelected.Count > 0)
            {
                foreach (var s in _multiSelected) map.Remove(s);
                _multiSelected.Clear();
            }
            else if (_selectedSection != null)
            {
                map.Remove(_selectedSection);
                _selectedSection = null;
            }

            EditorUtility.SetDirty(map);
            _window.OnSectionDeselected();
            MarkDirtyRepaint();
        }

        void RotateSelected()
        {
            var map = _window.CurrentMap;
            if (_selectedSection == null || map == null) return;

            if (!_selectedSection.entry.canRotate) return;

            Undo.RecordObject(map, "Rotate Section");
            _selectedSection.rotationSteps = (_selectedSection.rotationSteps + 1) % 4;
            EditorUtility.SetDirty(map);
            MarkDirtyRepaint();
            _window.OnSectionSelected(_selectedSection);
        }

        void ShowContextMenu(PlacedSection hit)
        {
            var menu = new GenericMenu();

            if (_multiSelected.Count > 1)
            {
                menu.AddItem(new GUIContent("Group as Room"), false, () =>
                    _window.GroupAsRoom(_multiSelected));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete All Selected"), false, DeleteSelected);
            }
            else if (hit != null)
            {
                _selectedSection = hit;
                _window.OnSectionSelected(hit);

                if (hit.entry.canRotate)
                    menu.AddItem(new GUIContent("Rotate 90° (R)"), false, RotateSelected);

                menu.AddItem(new GUIContent("Duplicate"), false, () =>
                {
                    var map = _window.CurrentMap;
                    if (map == null) return;
                    Undo.RecordObject(map, "Duplicate Section");
                    var dupe = new PlacedSection
                    {
                        entry = hit.entry,
                        sourceTheme = hit.sourceTheme,
                        gridPos = hit.gridPos + new Vector2Int(hit.RotatedSize.x, 0),
                        rotationSteps = hit.rotationSteps,
                        roomGroupId = hit.roomGroupId
                    };
                    map.Add(dupe);
                    EditorUtility.SetDirty(map);
                    MarkDirtyRepaint();
                });

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete (Del)"), false, DeleteSelected);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("No section here"));
            }

            menu.ShowAsContext();
        }
    }
}
