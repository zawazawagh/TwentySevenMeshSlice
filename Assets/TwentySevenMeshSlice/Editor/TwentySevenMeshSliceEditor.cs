using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace TwentySevenMeshSlice
{
    [CustomEditor(typeof(TwentySevenMeshSlice))]
    public partial class TwentySevenMeshSliceEditor : Editor
    {
        private bool _isEditing;
        private BoxBoundsHandle _innerBoxBoundsHandle, _outerBoxBoundsHandle;
        private TwentySevenMeshSlice _27MeshSlice;
        private readonly Color _guidePlaneCol = new Color(0f, 0f, 1f, 0.7f);
        private readonly Color _guidePlaneOutlineCol = new Color(0f, 1f, 1f, 1f);
        private bool _isMirrored;
        private bool _scaleMode;
        private bool _offsetLocked;
        private Tool _lastTool;
        private ToolbarButton _editBarButton;
        private ToolbarButton _mirrorBarButton;
        private ToolbarButton _resetBarButton;
        private ToolbarButton _scaleBarButton;
        private ToolbarButton _lockButton;
        private ToolbarButton _rollbackMeshBarButton;
        private ToolbarButton _saveBarButton;

        private SerializedProperty XMinI => serializedObject.FindProperty("_xMinI");
        private SerializedProperty YMinI => serializedObject.FindProperty("_yMinI");
        private SerializedProperty ZMinI => serializedObject.FindProperty("_zMinI");
        private SerializedProperty XMaxI => serializedObject.FindProperty("_xMaxI");
        private SerializedProperty YMaxI => serializedObject.FindProperty("_yMaxI");
        private SerializedProperty ZMaxI => serializedObject.FindProperty("_zMaxI");

        private SerializedProperty XMinO => serializedObject.FindProperty("_xMinO");
        private SerializedProperty YMinO => serializedObject.FindProperty("_yMinO");
        private SerializedProperty ZMinO => serializedObject.FindProperty("_zMinO");
        private SerializedProperty XMaxO => serializedObject.FindProperty("_xMaxO");
        private SerializedProperty YMaxO => serializedObject.FindProperty("_yMaxO");
        private SerializedProperty ZMaxO => serializedObject.FindProperty("_zMaxO");
        private SerializedProperty OriginalMesh => serializedObject.FindProperty("_originalMesh");
        private SerializedProperty SlicedMesh => serializedObject.FindProperty("_slicedMesh");

        private void OnEnable()
        {
            _isEditing = false;
            _isMirrored = false;
            _scaleMode = false;
            _offsetLocked = false;
            _lastTool = Tools.current;
            _27MeshSlice = serializedObject.targetObject as TwentySevenMeshSlice;
            Assert.IsNotNull(_27MeshSlice);
            _editBarButton = new ToolbarButton(OnEditButtonPressed, "EditCollider", "Edit Slice Bounds");
            _mirrorBarButton = new ToolbarButton(null, "Mirror", "Mirror edit mode");
            _resetBarButton = new ToolbarButton(ResetBoxBounds, "RotateTool", "Reset to original bounds");
            _scaleBarButton = new ToolbarButton(OnScaleButtonPressed, "ScaleTool", "Scale Mesh");
            _lockButton = new ToolbarButton(null, "LockIcon-On", "Lock Inner-Outer box offset");
            _rollbackMeshBarButton = new ToolbarButton(OnRollBackButtonPressed, "back", "Rollback to original mesh");
            _saveBarButton = new ToolbarButton(SaveMesh, "SaveAs", "Save mesh");
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            Tools.current = _lastTool;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnEditButtonPressed()
        {
            if (_isEditing)
            {
                _lastTool = Tools.current;
                Tools.current = Tool.None;
                GenerateBoxBounds();
            }
            else
            {
                Tools.current = _lastTool;
                _isMirrored = false;
                _scaleMode = false;
                _offsetLocked = false;
            }
        }

        private void OnScaleButtonPressed()
        {
            if (_scaleMode)
            {
                _27MeshSlice.DefineVertices();
            }
        }

        private void ResetBoxBounds()
        {
            var originalMesh = OriginalMesh.objectReferenceValue as Mesh;
            if (originalMesh != null)
            {
                _27MeshSlice.SetOuterBoxSize(originalMesh.bounds.size, originalMesh.bounds.center);
                _27MeshSlice.SetInnerBoxSize(originalMesh.bounds.size / 2, originalMesh.bounds.center);
            }

            UpdateBoxBounds();
        }

        private void OnRollBackButtonPressed()
        {
            _27MeshSlice.Init();
            GenerateBoxBounds();
            _scaleMode = false;
        }

        private void SaveMesh()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Mesh", _27MeshSlice.MeshFilter.sharedMesh.name, "prefab", "");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var sharedMesh = _27MeshSlice.MeshFilter.sharedMesh;
            Mesh mesh = Instantiate(sharedMesh);
            mesh.name = _27MeshSlice.MeshFilter.sharedMesh.name;
            
            SlicedMesh.objectReferenceValue = mesh;
            _27MeshSlice.MeshFilter.sharedMesh = mesh;

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(_27MeshSlice.gameObject, path);
            asset.GetComponent<MeshFilter>().sharedMesh = mesh;
            
            var fieldInfo = typeof(TwentySevenMeshSlice).GetField(
                "_slicedMesh",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fieldInfo == null)
            {
                return;
            }
            
            fieldInfo.SetValue(asset.GetComponent<TwentySevenMeshSlice>(), mesh);
            AssetDatabase.Refresh();
            AssetDatabase.AddObjectToAsset(mesh, asset);
            PrefabUtility.SavePrefabAsset(asset);
            EditorGUIUtility.PingObject(asset);

            SlicedMesh.objectReferenceValue = sharedMesh;
            _27MeshSlice.MeshFilter.sharedMesh = sharedMesh;
        }

        private void GenerateBoxBounds()
        {
            Mesh mesh = _27MeshSlice.MeshFilter.sharedMesh;
            Assert.IsNotNull(mesh);
            _innerBoxBoundsHandle = new BoxBoundsHandle();
            _innerBoxBoundsHandle.midpointHandleDrawFunction = (id, position, rotation, size, type) =>
            {
                if (id == GUIUtility.hotControl)
                {
                    CompareFunction currentZTest = Handles.zTest;
                    Handles.zTest = CompareFunction.LessEqual;
                    Matrix4x4 trs = Handles.matrix * Matrix4x4.TRS(position, rotation, Vector3.Scale(_27MeshSlice.transform.lossyScale, _27MeshSlice.GetOuterBoxSize() / 2));
                    using (new Handles.DrawingScope(trs))
                    {
                        Handles.DrawSolidRectangleWithOutline(new[]
                                                              {
                                                                  Vector3.left + Vector3.down, Vector3.left + Vector3.up,
                                                                  Vector3.up + Vector3.right, Vector3.right + Vector3.down
                                                              },
                                                              _guidePlaneCol,
                                                              _guidePlaneOutlineCol);
                    }

                    Handles.zTest = currentZTest;
                }

                Handles.DotHandleCap(id, position, rotation, size, type);
            };
            _outerBoxBoundsHandle = new BoxBoundsHandle();
            UpdateBoxBounds();
        }

        private void UpdateBoxBounds()
        {
            if (_innerBoxBoundsHandle != null && _outerBoxBoundsHandle != null)
            {
                _outerBoxBoundsHandle.center = _27MeshSlice.GetOuterBoxCenter();
                _outerBoxBoundsHandle.size = _27MeshSlice.GetOuterBoxSize();
                _innerBoxBoundsHandle.center = _27MeshSlice.GetInnerBoxCenter();
                _innerBoxBoundsHandle.size = _27MeshSlice.GetInnerBoxSize();
                SceneView.RepaintAll();
            }
        }

        private void OnUndoRedoPerformed()
        {
            if (_scaleMode)
            {
                _27MeshSlice.UpdateMeshScale();
            }

            UpdateBoxBounds();
        }
    }
}
