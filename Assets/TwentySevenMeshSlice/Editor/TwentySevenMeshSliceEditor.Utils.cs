using System;
using UnityEditor;
using UnityEngine;

namespace TwentySevenMeshSlice
{
    public partial class TwentySevenMeshSliceEditor
    {
        private static Vector3 InvertScaleVector(Vector3 scaleVector)
        {
            for (int axis = 0; axis < 3; ++axis)
                scaleVector[axis] = scaleVector[axis] == 0f ? 0f : 1f / scaleVector[axis];

            return scaleVector;
        }

        private static Vector3 TransforMeshCenterToHandleSpace(Transform objectTransform, Vector3 boxCenter)
        {
            return Handles.inverseMatrix * (objectTransform.localToWorldMatrix * boxCenter);
        }

        private static Vector3 TransformHandleCenterToMeshSpace(Transform colliderTransform, Vector3 boxCenter)
        {
            return colliderTransform.localToWorldMatrix.inverse * (Handles.matrix * boxCenter);
        }

        private static GUIContent ToolbarButtonIcon(string iconName, string hoverText)
        {
            GUIContent content;
            Texture2D targetIcon = EditorGUIUtility.FindTexture(iconName);
            if (targetIcon == null)
            {
                content = new GUIContent("?", hoverText);
            }
            else
            {
                content = new GUIContent(targetIcon, hoverText);
            }

            return new GUIContent(content);
        }

        private class ToolbarButton
        {
            private readonly string _iconName;
            private readonly string _hoverText;

            public ToolbarButton(Action buttonEvent, string iconName, string hoverText)
            {
                OnButtonPressed = buttonEvent;
                _iconName = iconName;
                _hoverText = hoverText;
            }

            private event Action OnButtonPressed;

            public void DrawButton(float xMin)
            {
                if (Event.current != null)
                {
                    var dummy = false;
                    DrawButton(ref dummy, xMin);
                }
            }

            public void DrawButton(ref bool status, float xMin)
            {
                Rect controlRect = EditorGUILayout.GetControlRect(true, 23f, "Button");
                var position1 = new Rect(xMin, controlRect.yMin, 33f, 23f);
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    status = GUI.Toggle(position1, status, ToolbarButtonIcon(_iconName, _hoverText), "Button");

                    if (check.changed)
                    {
                        OnButtonPressed?.Invoke();
                        SceneView.RepaintAll();
                    }
                }
            }
        }
    }
}
