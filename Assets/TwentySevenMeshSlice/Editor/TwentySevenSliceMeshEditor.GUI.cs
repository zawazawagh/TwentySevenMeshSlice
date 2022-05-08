using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace TwentySevenMeshSlice
{
    public partial class TwentySevenMeshSliceEditor
    {
        protected void OnSceneGUI()
        {
            if (!_isEditing || _innerBoxBoundsHandle == null || _outerBoxBoundsHandle == null)
            {
                return;
            }

            Color col = _scaleMode ? Color.cyan : Color.red;
            Handles.color = col;
            if (_offsetLocked)
            {
                DrawBoxBounds(_outerBoxBoundsHandle, _27MeshSlice.transform, _27MeshSlice.GetOuterBoxCenter(), _27MeshSlice.SetEntireSize);
            }
            else
            {
                DrawBoxBounds(_outerBoxBoundsHandle, _27MeshSlice.transform, _27MeshSlice.GetOuterBoxCenter(), _27MeshSlice.SetOuterBoxSize);
            }

            col = _offsetLocked ? Color.gray : col;
            Handles.color = col;
            if (_offsetLocked)
            {
                DrawBoxBounds(_innerBoxBoundsHandle, _27MeshSlice.transform, _27MeshSlice.GetInnerBoxCenter(), null);
            }
            else
            {
                DrawBoxBounds(_innerBoxBoundsHandle, _27MeshSlice.transform, _27MeshSlice.GetInnerBoxCenter(), _27MeshSlice.SetInnerBoxSize);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(OriginalMesh);
                if (check.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    _27MeshSlice.Init();
                    ResetBoxBounds();
                    if (_isEditing)
                    {
                        _27MeshSlice.DefineVertices();
                        UpdateBoxBounds();
                    }
                }
            }

            var originalMesh = OriginalMesh.objectReferenceValue as Mesh;
            if (!originalMesh)
            {
                _isEditing = false;
                _isMirrored = false;
                _scaleMode = false;
                return;
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                float xMin = EditorGUIUtility.currentViewWidth / 2 - 33f * 3.5f;
                _editBarButton.DrawButton(ref _isEditing, xMin);
                using (new EditorGUI.DisabledScope(!_isEditing))
                {
                    xMin += 33f;
                    _mirrorBarButton.DrawButton(ref _isMirrored, xMin);
                    xMin += 33f;
                    _scaleBarButton.DrawButton(ref _scaleMode, xMin);
                    xMin += 33f;
                    _lockButton.DrawButton(ref _offsetLocked, xMin);
                    xMin += 33f;
                    _resetBarButton.DrawButton(xMin);
                    xMin += 33f;
                    _rollbackMeshBarButton.DrawButton(xMin);
                }

                xMin += 33f;
                _saveBarButton.DrawButton(xMin);
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(" ");
                GUILayout.Label("MinOuter");
                GUILayout.Label("MinInner");
                GUILayout.Label("MaxInner");
                GUILayout.Label("MaxOuter");
            }

            DrawBoundsFields(XMinO, XMinI, XMaxI, XMaxO);
            DrawBoundsFields(YMinO, YMinI, YMaxI, YMaxO);
            DrawBoundsFields(ZMinO, ZMinI, ZMaxI, ZMaxO);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBoxBounds(BoxBoundsHandle handle, Transform meshSpace, Vector3 meshCenter, Action<Vector3, Vector3> setBoxSize)
        {
            Matrix4x4 trs = Handles.matrix * Matrix4x4.TRS(meshSpace.position, meshSpace.rotation, Vector3.one);
            var lossyScale = meshSpace.lossyScale;
            using (new Handles.DrawingScope(trs))
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var defaultSize = handle.size;
                var defaultCenter = handle.center;
                handle.center = TransforMeshCenterToHandleSpace(meshSpace, handle.center);
                handle.size = Vector3.Scale(handle.size, lossyScale);
                handle.DrawHandle();
                if (check.changed)
                {
                    Undo.RecordObject(target, "Changed BoxHandle");
                    handle.center = _isMirrored ? meshCenter : TransformHandleCenterToMeshSpace(meshSpace, handle.center);
                    handle.size = Vector3.Scale(handle.size, InvertScaleVector(lossyScale));
                    setBoxSize?.Invoke(handle.size, handle.center);
                    if (_scaleMode)
                    {
                        _27MeshSlice.UpdateMeshScale();
                    }

                    UpdateBoxBounds();
                }
                else
                {
                    handle.size = defaultSize;
                    handle.center = defaultCenter;
                }
            }
        }

        private void DrawBoundsFields(SerializedProperty val0, SerializedProperty val1, SerializedProperty val2, SerializedProperty val3)
        {
            string axisName = val0.name.Substring(1, 1);
            using (new EditorGUILayout.HorizontalScope())
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                GUILayout.Label(axisName);
                EditorGUILayout.PropertyField(val0, GUIContent.none);
                EditorGUILayout.PropertyField(val1, GUIContent.none);
                EditorGUILayout.PropertyField(val2, GUIContent.none);
                EditorGUILayout.PropertyField(val3, GUIContent.none);
                if (check.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    if (_scaleMode)
                    {
                        _27MeshSlice.UpdateMeshScale();
                    }

                    UpdateBoxBounds();
                }
            }
        }
    }
}
