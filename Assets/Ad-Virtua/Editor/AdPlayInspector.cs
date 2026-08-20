using UnityEngine;
using UnityEditor;
using Ad_Virtua.Runtime;

namespace Ad_Virtua.Editor
{
    /// <summary>
    /// Custom Inspector for AdPlay component
    /// Displays runtime information, survey feature settings, and documentation links in the inspector
    /// </summary>
    [CustomEditor(typeof(AdPlay))]
    public class AdPlayInspector : UnityEditor.Editor
    {
        private SerializedProperty advirtuaUnitIdProp;
        private SerializedProperty targetCameraProp;
        private SerializedProperty enableSurveyProp;
        private SerializedProperty surveyCoverageThresholdProp;
        private SerializedProperty onSurveyUIShownProp;
        private SerializedProperty onSurveyAnsweredProp;

        // Conversion Feature Properties
        private SerializedProperty enableConversionProp;
        private SerializedProperty onConversionUIShownProp;
        private SerializedProperty onConversionUIClosedProp;


        // Variables for scale adjustment
        private float monitorScale = 1.0f;
        private Vector3 initialScale;
        private bool scaleInitialized = false;

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;

            // Get SerializedProperty
            advirtuaUnitIdProp = serializedObject.FindProperty("advirtuaUnitId");
            targetCameraProp = serializedObject.FindProperty("targetCamera");
            enableSurveyProp = serializedObject.FindProperty("enableSurvey");
            surveyCoverageThresholdProp = serializedObject.FindProperty("surveyCoverageThreshold");
            onSurveyUIShownProp = serializedObject.FindProperty("OnSurveyUIShown");
            onSurveyAnsweredProp = serializedObject.FindProperty("OnSurveyAnswered");

            // Conversion Feature Properties
            enableConversionProp = serializedObject.FindProperty("enableConversion");
            onConversionUIShownProp = serializedObject.FindProperty("OnConversionUIShown");
            onConversionUIClosedProp = serializedObject.FindProperty("OnConversionUIClosed");

        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AdPlay adPlay = (AdPlay)target;

            // === 1. Basic Settings ===
            EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(advirtuaUnitIdProp, new GUIContent("AdVirtua Unit ID"));
            EditorGUILayout.PropertyField(targetCameraProp, new GUIContent("Target Camera"));

            EditorGUILayout.Space(10);

            // === 2. Monitor Scale ===
            EditorGUILayout.LabelField("Monitor Scale", EditorStyles.boldLabel);
            DrawMonitorScaleControl(adPlay);

            EditorGUILayout.Space(10);

            // === 3. Runtime Information ===
            EditorGUILayout.LabelField("Runtime Information", EditorStyles.boldLabel);
            GUI.enabled = false;
            EditorGUILayout.FloatField("Screen Coverage (%)", adPlay.ScreenCoveragePercent);
            GUI.enabled = true;

            if (Application.isPlaying)
            {
                var status = adPlay.GetCurrentViewingValidationStatus();
                bool allOk = status.CanCount;

                if (allOk)
                {
                    var icon = EditorGUIUtility.IconContent("d_winbtn_mac_max");
                    icon.text = "  Viewing time counting";
                    EditorGUILayout.LabelField(icon, GUILayout.Height(20));
                }
                else
                {
                    string ngItems = BuildNgList(status);
                    EditorGUILayout.HelpBox("Viewing conditions NG:\n" + ngItems, MessageType.Warning);
                }
            }

            EditorGUILayout.Space(10);

            // === 4. Survey Feature Settings ===
            EditorGUILayout.LabelField("Survey Feature Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableSurveyProp, new GUIContent("Enable Survey Feature"));

            if (enableSurveyProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(surveyCoverageThresholdProp, new GUIContent("Coverage Threshold (%)"));
                EditorGUI.indentLevel--;
                EditorGUILayout.PropertyField(onSurveyUIShownProp, new GUIContent("On Survey UI Shown"));
                EditorGUILayout.PropertyField(onSurveyAnsweredProp, new GUIContent("On Survey Answered"));
            }

            EditorGUILayout.Space(10);

            // === 5. Conversion Feature Settings ===
            EditorGUILayout.LabelField("Conversion Feature Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableConversionProp, new GUIContent("Enable Conversion Feature"));

            if (enableConversionProp.boolValue)
            {
                EditorGUILayout.PropertyField(onConversionUIShownProp, new GUIContent("On Conversion UI Shown"));
                EditorGUILayout.PropertyField(onConversionUIClosedProp, new GUIContent("On Conversion UI Closed"));
            }

            EditorGUILayout.Space(10);

            // === 6. Editor Only ===
            EditorGUILayout.LabelField("Editor Only", EditorStyles.boldLabel);

            bool isPlaying = Application.isPlaying;

            if (!isPlaying)
            {
                EditorGUILayout.HelpBox("Only works in Play Mode", MessageType.Warning);
            }

            EditorGUILayout.Space(5);

            // Survey UI Test
            EditorGUILayout.LabelField("Survey UI Test", EditorStyles.miniBoldLabel);
            bool isSurveyDisplayed = adPlay.IsSurveyUIDisplayed;

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = isPlaying && !isSurveyDisplayed;
            if (GUILayout.Button("Show", GUILayout.Height(22)))
            {
                adPlay.ShowDummySurveyForEditor();
            }
            GUI.enabled = isPlaying && isSurveyDisplayed;
            if (GUILayout.Button("Hide", GUILayout.Height(22)))
            {
                adPlay.HideDummySurveyForEditor();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (isPlaying && isSurveyDisplayed)
            {
                EditorGUILayout.HelpBox("Displaying", MessageType.None);
            }

            EditorGUILayout.Space(5);

            // Conversion UI Test
            EditorGUILayout.LabelField("Conversion UI Test", EditorStyles.miniBoldLabel);
            bool isConversionDisplayed = adPlay.IsConversionUIDisplayed;

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = isPlaying && !isConversionDisplayed;
            if (GUILayout.Button("Show", GUILayout.Height(22)))
            {
                adPlay.ShowDummyConversionForEditor();
            }
            GUI.enabled = isPlaying && isConversionDisplayed;
            if (GUILayout.Button("Hide", GUILayout.Height(22)))
            {
                adPlay.HideDummyConversionForEditor();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (isPlaying && isConversionDisplayed)
            {
                EditorGUILayout.HelpBox("Displaying", MessageType.None);
            }

            EditorGUILayout.Space(10);

            // === 8. External Link ===
            EditorGUILayout.LabelField("External Link", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Docs", GUILayout.Height(18)))
            {
                Application.OpenURL("https://docs.ad-virtua.com");
            }
            if (GUILayout.Button("Dashboard", GUILayout.Height(18)))
            {
                Application.OpenURL("https://app.ad-virtua.com/developer/dashboard");
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private string BuildNgList(ViewingValidationStatus s)
        {
            var lines = new System.Text.StringBuilder();
            if (!s.NetworkConnected)    lines.AppendLine($"  [×] Network ({s.CurrentNetworkStatus})");
            if (!s.ActualMoviePlaying)  lines.AppendLine($"  [×] Movie ({s.CurrentMovieType})");
            if (!s.PlayStatusOK)        lines.AppendLine($"  [×] Play Status ({s.CurrentPlayStatus})");
            if (!s.HasPrepared)         lines.AppendLine("  [×] Has Prepared");
            if (!s.CameraAngleOK)       lines.AppendLine($"  [×] Camera Angle ({s.CurrentCameraAngle:F1}°)");
            if (!s.AspectRatioOK)       lines.AppendLine("  [×] Aspect Ratio");
            if (!s.NegativeScaleOK)     lines.AppendLine("  [×] Negative Scale");
            if (!s.ScreenCoverageOK)    lines.AppendLine($"  [×] Screen Coverage ({s.CurrentScreenCoverage:F1}%)");
            if (!s.AllCornersVisibleOK) lines.AppendLine($"  [×] All Corners Visible ({s.CurrentVisibleCorners}/4)");
            return lines.ToString().TrimEnd();
        }

        /// <summary>
        /// UI for adjusting monitor scale while maintaining aspect ratio
        /// </summary>
        private void DrawMonitorScaleControl(AdPlay adPlay)
        {
            Transform monitorTransform = adPlay.transform;

            // Set initial scale only on first time (16:9 aspect ratio)
            if (!scaleInitialized)
            {
                initialScale = new Vector3(16f, 9f, 1f);
                // Calculate scale multiplier from current scale (based on X axis)
                monitorScale = monitorTransform.localScale.x / initialScale.x;
                scaleInitialized = true;
            }

            EditorGUI.BeginChangeCheck();

            // Adjust scale multiplier with slider (0.01 ~ 5.0x)
            monitorScale = EditorGUILayout.Slider(
                new GUIContent("Scale Multiplier", "Adjust monitor scale while maintaining aspect ratio"),
                monitorScale,
                0.01f,
                5.0f
            );

            if (EditorGUI.EndChangeCheck())
            {
                // Support Undo
                Undo.RecordObject(monitorTransform, "Monitor Scale Change");

                // Apply by multiplying initial scale with multiplier
                monitorTransform.localScale = initialScale * monitorScale;

                // Update scene view
                EditorUtility.SetDirty(monitorTransform);
            }

            // Display current scale value (ReadOnly)
            GUI.enabled = false;
            EditorGUILayout.Vector3Field("Current Scale", monitorTransform.localScale);
            GUI.enabled = true;

            // Reset button
            if (GUILayout.Button("Reset to Original Scale"))
            {
                Undo.RecordObject(monitorTransform, "Reset Monitor Scale");
                monitorTransform.localScale = initialScale;
                monitorScale = 1.0f;
                EditorUtility.SetDirty(monitorTransform);
            }
        }
    }
}
