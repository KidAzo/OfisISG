using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NGS.SLO.Shared
{
    public static class SLOGUI
    {
        public static GUIStyle TitleLabelStyle
        {
            get
            {
                CreateGUIStylesIfNull();

                return _titleLabelStyle;
            }
        }
        public static GUIStyle FoldoutGUIStyle
        {
            get
            {
                CreateGUIStylesIfNull();

                return _foldoutGUIStyle;
            }
        }
        public static GUIStyle ButtonGUIStyle
        {
            get
            {
                CreateGUIStylesIfNull();

                return _buttonGUIStyle;
            }
        }

        private static GUIStyle _titleLabelStyle;
        private static GUIStyle _foldoutGUIStyle;
        private static GUIStyle _buttonGUIStyle;


        public static void DrawSeparatorLine(float thickness = 1, float padding = 2)
        {
            Rect previousRect = GUILayoutUtility.GetLastRect();

            GUILayout.Space(padding);

            EditorGUILayout.LabelField("", GUILayout.Height(thickness));

            Rect lineRect = GUILayoutUtility.GetLastRect();

            lineRect.x = previousRect.x;
            lineRect.width = previousRect.width;

            EditorGUI.DrawRect(lineRect, Color.gray);

            GUILayout.Space(padding);
        }

        public static void DrawUnderlinedText(string text, GUIStyle style, float thickness = 1, float padding = 2, float linePadding = 5)
        {
            EditorGUILayout.LabelField(text, style);

            Rect previousRect = GUILayoutUtility.GetLastRect();

            GUILayout.Space(padding);

            EditorGUILayout.LabelField("", GUILayout.Height(thickness));
            Rect lineRect = GUILayoutUtility.GetLastRect();

            float textWidth = style.CalcSize(new GUIContent(text)).x + linePadding;

            float x;

            if (style.alignment == TextAnchor.UpperLeft ||
                style.alignment == TextAnchor.MiddleLeft ||
                style.alignment == TextAnchor.LowerLeft)
            {
                x = previousRect.x; 
            }
            else if (style.alignment == TextAnchor.UpperCenter ||
                     style.alignment == TextAnchor.MiddleCenter ||
                     style.alignment == TextAnchor.LowerCenter)
            {
                x = previousRect.x + (previousRect.width - textWidth) / 2; 
            }
            else 
            {
                x = previousRect.x + previousRect.width - textWidth; 
            }

            lineRect.x = x;
            lineRect.width = textWidth;

            EditorGUI.DrawRect(lineRect, Color.gray);

            GUILayout.Space(padding);
        }

        private static void CreateGUIStylesIfNull()
        {
            if (_titleLabelStyle == null)
            {
                _titleLabelStyle = new GUIStyle();
                _titleLabelStyle.fontSize = 20;
                _titleLabelStyle.fontStyle = FontStyle.Bold;
                _titleLabelStyle.alignment = TextAnchor.MiddleLeft;
                _titleLabelStyle.normal.textColor = Color.white;
            };

            if (_foldoutGUIStyle == null)
            {
                _foldoutGUIStyle = new GUIStyle(EditorStyles.foldout);
                _foldoutGUIStyle.fontSize = 15;
                _foldoutGUIStyle.fontStyle = FontStyle.Bold;
                _foldoutGUIStyle.normal.textColor = Color.white;
            }

            if (_buttonGUIStyle == null)
            {
                _buttonGUIStyle = new GUIStyle(GUI.skin.button);
                _buttonGUIStyle.fontSize = 12;
                _buttonGUIStyle.fixedHeight = 30;
                _buttonGUIStyle.margin = new RectOffset(5, 5, 5, 5);
                _buttonGUIStyle.border = new RectOffset(0, 0, 0, 0);
                _buttonGUIStyle.padding = new RectOffset(5, 5, 5, 5);
            }
        }
    }
}
