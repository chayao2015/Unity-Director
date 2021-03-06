﻿using Tangzx.Director;
using UnityEditor;
using UnityEngine;

namespace TangzxInternal
{
    /// <summary>
    /// 过场编辑器
    /// </summary>
    class SequencerEditorWindow : DirectorWindow
    {
        [MenuItem("Tools/Director/SequencerEditor")]
        static void Open()
        {
            GetWindow<SequencerEditorWindow>("Sequencer");
        }

        private GameObject _dataGO;
        private SequencerData _data;
        private SequencerCategory _category;
        private SequencerPlayer _player;

        protected override void InitHierarchy()
        {
            base.InitHierarchy();
            eventHierarchy.treeView.dragging = new EventTreeViewDragging(eventHierarchy.treeView, this);
        }

        protected override void OnCheckDataGUI()
        {
            GameObject selectGO = Selection.activeGameObject;

            if (_dataGO != selectGO)
            {
                SequencerData data = null;
                if (selectGO)
                {
                    data = selectGO.GetComponent<SequencerData>();
                    if (data == null && GUILayout.Button("Create"))
                    {
                        data = selectGO.AddComponent<SequencerData>();
                    }
                }

                if (data)
                {
                    _dataGO = selectGO;
                    SetData(data);
                }
                else
                {
                    _dataGO = null;
                    SetData(null);
                }
            }

            if (_category == null && _data)
                SetCategory(_data.defaultCategory);

            if (treeRootItem == null)
            {
                ShowNotification(new GUIContent("Select GameObject"));
            }
        }

        void SetData(SequencerData sd)
        {
            _data = sd;
            if (sd)
            {
                SetCategory(sd.defaultCategory);
            }
            else
            {
                SetCategory(null);
            }
        }

        void ClampRange()
        {
            float totalDuration = _category ? _category.totalDuration : 5;

            eventSheetEditor.hRangeMax = totalDuration;
            eventSheetEditor.SetShownHRangeInsideMargins(0, totalDuration);
            playHeadTime = Mathf.Min(playHeadTime, totalDuration);
        }

        protected override void OnToolbarGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbarButton);
            {
                EditorGUI.BeginDisabledGroup(_data == null);
                {
                    if (GUILayout.Button("Create", EditorStyles.toolbarButton))
                    {
                        HandleCreateCategory();
                    }

                    if (GUILayout.Button("Remove", EditorStyles.toolbarButton))
                    {
                        HandleRemoveCategroy();
                    }

                    //is preview
                    EditorGUI.BeginChangeCheck();
                    windowState.isPreview = GUILayout.Toggle(windowState.isPreview, AnimationWindowStyles.playContent, EditorStyles.toolbarButton);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (!windowState.isPreview) StopPreview();
                    }

                    GUILayout.FlexibleSpace();
                }
                EditorGUI.EndDisabledGroup();
            }
            GUILayout.EndHorizontal();
        }

        protected override void OnHierarchyGUI(Rect rect)
        {
            EditorGUI.BeginDisabledGroup(windowState.isPreview);
            {
                Rect treeRect = rect;
                treeRect.yMin += DirectorWindowState.TOOLBAR_HEIGHT;
                base.OnHierarchyGUI(treeRect);
                //toolbar
                rect.height = DirectorWindowState.TOOLBAR_HEIGHT;
                GUILayout.BeginArea(rect);
                {
                    GUILayout.BeginHorizontal(EditorStyles.toolbar);
                    {
                        if (_category)
                        {
                            if (GUILayout.Button(_category.categoryName, EditorStyles.toolbarPopup, GUILayout.Width(100)))
                            {
                                ShowCategoryMenu();
                            }
                            //total duration
                            EditorGUI.BeginChangeCheck();
                            string totalDuration = _category.totalDuration.ToString();
                            totalDuration = GUILayout.TextField(totalDuration, EditorStyles.toolbarTextField, GUILayout.Width(30));
                            if (EditorGUI.EndChangeCheck())
                            {
                                int duration = 1;
                                int.TryParse(totalDuration, out duration);
                                duration = Mathf.Max(1, duration);
                                _category.totalDuration = Mathf.Max(1, duration);
                                ClampRange();
                            }
                        }
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndArea();
            }
            EditorGUI.EndDisabledGroup();
            //Menu
            /*if (Event.current.type == EventType.ContextClick && treeRect.Contains(Event.current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Create Category"), false, HandleCreateCategory);
                menu.AddItem(new GUIContent("Remove Category"), false, HandleRemoveCategroy);
                menu.ShowAsContext();
            }*/
        }

        void HandleCreateCategory()
        {
            CreateCategoryWindow w = GetWindow<CreateCategoryWindow>();
            w.mainWindow = this;
        }

        void HandleRemoveCategroy()
        {
            if (_category && EditorUtility.DisplayDialog("警告", "确认删除分类：[" + _category.categoryName + "] ?", "确定", "取消"))
            {
                _data.RemoveCategory(_category);
                _category = null;
            }
        }

        void ShowCategoryMenu()
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < _data.categories.Count; i++)
            {
                SequencerCategory sc = _data.categories[i];
                menu.AddItem(new GUIContent(sc.categoryName), false, () =>
                    {
                        SetCategory(sc);
                    });
            }
            menu.ShowAsContext();
        }

        void SetCategory(SequencerCategory sc)
        {
            StopPreview();

            if (sc)
                treeRootItem = new SequencerCategoryTreeItem(sc);
            else
                treeRootItem = null;
            _category = sc;

            ClampRange();
        }

        public DragAndDropVisualMode DoDrag(GameObject[] objs)
        {
            for (int i = 0; i < objs.Length; i++)
            {
                GameObject go = objs[i];
                SequencerEventContainer ec = null;
                var e = _category.GetEnumerator();
                while (e.MoveNext())
                {
                    if (e.Current.attach == go.transform)
                    {
                        ec = e.Current;
                        break;
                    }
                }

                if (ec == null)
                {
                    ec = _data.CreateSubAsset<SequencerEventContainer>(HideFlags.HideInInspector);
                    ec.attach = go.transform;
                    _category.AddContainer(ec);
                }
            }

            windowState.ReloadData();

            return DragAndDropVisualMode.Move;
        }

        public void CreateCategory(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var e = _data.GetEnumerator();
                while (e.MoveNext())
                {
                    if (e.Current.categoryName == name)
                        return;
                }

                SequencerCategory sc = _data.CreateSubAsset<SequencerCategory>(HideFlags.HideInInspector);
                sc.categoryName = name;
                _data.AddCategory(sc);
                windowState.ReloadData();
                if (_category == null)
                    SetCategory(sc);
            }
        }

        void UpdatePreview(bool init = false)
        {
            if (_data == null || _category == null)
                return;
            
            if (windowState.isPreview)
            {
                if (_player == null)
                    _player = _data.GetComponent<SequencerPlayer>();
                if (_player)
                {
                    if (init)
                    {
                        _player.ReadyToPlay();
                        _player.Play(_category);
                    }
                    ProcessPreview();
                }
            }
            else
            {
                StopPreview();
            }
        }

        void ProcessPreview()
        {
            if (_player)
            {
                _player.Process(playHeadTime);
                SceneView.RepaintAll();
            }
        }

        void StopPreview()
        {
            windowState.isPreview = false;
            if (_player)
            {
                _player.StopAndRecover();
                _player = null;
            }
        }

        public override void SetPlayHead(float value)
        {
            base.SetPlayHead(value);
            UpdatePreview();
        }

        public override void OnDragPlayHeadStart()
        {
            if (windowState.isPreview)
                UpdatePreview(true);
        }

        public override void OnDragPlayHeadEnd()
        {
            base.OnDragPlayHeadEnd();
            if (windowState.isPreview && _player)
            {
                _player.StopAndRecover();
                _player = null;
            }
        }

        protected override void OnPlayModeStateChanged()
        {
            base.OnPlayModeStateChanged();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopPreview();
            }
        }
    }
}