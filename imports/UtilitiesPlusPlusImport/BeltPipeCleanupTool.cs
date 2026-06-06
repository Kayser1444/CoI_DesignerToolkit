using System;
using System.Collections.Generic;
using System.Reflection;
using Mafi;
using Mafi.Core.Entities;
using Mafi.Core.Factory.Transports;
using UnityEngine;

namespace UtilitiesPP
{
    public class BeltPipeCleanupTool
    {
        private readonly EntitiesManager _entitiesManager;
        private readonly DragRectRenderer _rectRenderer;

        private bool _active;
        private Vector2 _dragStartScreen;
        private Vector2 _dragCurrentScreen;
        private bool _dragStarted;

        private object _highlighter;
        private MethodInfo _highlightMethod;
        private MethodInfo _removeHighlightMethod;
        private readonly Dictionary<int, Transport> _highlighted = new Dictionary<int, Transport>();
        private readonly List<Transport> _matches = new List<Transport>();

        public bool IsActive => _active;
        public Action<string> OnStatusChanged;
        public Action OnExit;

        private static readonly ColorRgba CLEANUP_HIGHLIGHT = new ColorRgba(230, 80, 80);

        public BeltPipeCleanupTool(EntitiesManager entitiesManager, DragRectRenderer rectRenderer)
        {
            _entitiesManager = entitiesManager;
            _rectRenderer = rectRenderer;
        }

        public void SetHighlighter(object highlighter)
        {
            _highlighter = highlighter;
            if (highlighter != null)
            {
                var type = highlighter.GetType();
                _highlightMethod = type.GetMethod("Highlight",
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new Type[] { typeof(IRenderedEntity), typeof(ColorRgba) }, null);
                _removeHighlightMethod = type.GetMethod("RemoveHighlight",
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new Type[] { typeof(IRenderedEntity) }, null);
            }
        }

        public void Activate()
        {
            _active = true;
            _dragStarted = false;
            ClearHighlights();
        }

        public void Deactivate()
        {
            bool wasActive = _active;
            _active = false;
            _dragStarted = false;
            _rectRenderer.Hide();
            ClearHighlights();
            OnStatusChanged?.Invoke("");
            if (wasActive) OnExit?.Invoke();
        }

        public void UpdateInput()
        {
            if (!_active) return;

            if (_dragStarted && Input.GetMouseButtonDown(1))
            {
                _dragStarted = false;
                _rectRenderer.Hide();
                ClearHighlights();
                return;
            }

            if (!_dragStarted && Input.GetMouseButtonDown(0))
            {
                _dragStartScreen = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                _dragCurrentScreen = _dragStartScreen;
                _dragStarted = true;
                _rectRenderer.Show();
                ComputeMatchesAndUpdateHighlights();
            }

            if (_dragStarted && Input.GetMouseButton(0))
            {
                var current = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (current != _dragCurrentScreen)
                {
                    _dragCurrentScreen = current;
                    _rectRenderer.SetRect(_dragStartScreen, current);
                    ComputeMatchesAndUpdateHighlights();
                }
            }

            if (_dragStarted && Input.GetMouseButtonUp(0))
            {
                _rectRenderer.Hide();
                _dragStarted = false;

                int count = ApplyMatches();
                ClearHighlights();
                OnStatusChanged?.Invoke(ModTranslation.GetFmt("Removed {0} belts/pipes", count));
            }
        }

        private void ComputeMatchesAndUpdateHighlights()
        {
            _matches.Clear();

            float minX = Mathf.Min(_dragStartScreen.x, _dragCurrentScreen.x);
            float maxX = Mathf.Max(_dragStartScreen.x, _dragCurrentScreen.x);
            float minY = Mathf.Min(_dragStartScreen.y, _dragCurrentScreen.y);
            float maxY = Mathf.Max(_dragStartScreen.y, _dragCurrentScreen.y);

            var cam = Camera.main;
            if (cam == null) { SyncHighlights(); return; }

            var inRectIds = new HashSet<int>();
            try
            {
                foreach (var t in _entitiesManager.GetAllEntitiesOfType<Transport>())
                {
                    if (t == null || t.IsDestroyed) continue;
                    if (t.IsFullyConnected) continue;

                    Tile3i tile = t.CenterTile;
                    Vector3 worldPos = new Vector3(tile.X * 2f, tile.Z * 2f, tile.Y * 2f);
                    Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
                    if (screenPos.z < 0) continue;

                    float sy = Screen.height - screenPos.y;
                    if (screenPos.x < minX || screenPos.x > maxX || sy < minY || sy > maxY) continue;

                    _matches.Add(t);
                    inRectIds.Add(t.Id.Value);
                }
            }
            catch
            {
            }

            SyncHighlights(inRectIds);
        }

        private void SyncHighlights(HashSet<int> wanted = null)
        {
            if (_highlighter == null || _highlightMethod == null || _removeHighlightMethod == null) return;

            if (wanted == null)
            {
                ClearHighlights();
                return;
            }

            try
            {
                var toRemove = new List<int>();
                foreach (var kv in _highlighted)
                    if (!wanted.Contains(kv.Key)) toRemove.Add(kv.Key);

                foreach (var id in toRemove)
                {
                    var ent = _highlighted[id] as IRenderedEntity;
                    if (ent != null)
                        try { _removeHighlightMethod.Invoke(_highlighter, new object[] { ent }); }
                        catch { }
                    _highlighted.Remove(id);
                }

                foreach (var t in _matches)
                {
                    int id = t.Id.Value;
                    if (_highlighted.ContainsKey(id)) continue;
                    var rend = t as IRenderedEntity;
                    if (rend == null) continue;
                    try
                    {
                        _highlightMethod.Invoke(_highlighter, new object[] { rend, CLEANUP_HIGHLIGHT });
                        _highlighted[id] = t;
                    }
                    catch { }
                }
            }
            catch
            {
            }
        }

        private void ClearHighlights()
        {
            if (_highlighted.Count == 0) return;
            if (_highlighter != null && _removeHighlightMethod != null)
            {
                foreach (var kv in _highlighted)
                {
                    var ent = kv.Value as IRenderedEntity;
                    if (ent == null) continue;
                    try { _removeHighlightMethod.Invoke(_highlighter, new object[] { ent }); }
                    catch { }
                }
            }
            _highlighted.Clear();
        }

        private int ApplyMatches()
        {
            int succeeded = 0;
            var snapshot = new List<Transport>(_matches);
            foreach (var t in snapshot)
            {
                try
                {
                    if (t == null || t.IsDestroyed) continue;
                    _entitiesManager.RemoveAndDestroyEntityNoChecks(t, EntityRemoveReason.Remove);
                    succeeded++;
                }
                catch
                {
                }
            }
            _matches.Clear();
            return succeeded;
        }
    }
}
