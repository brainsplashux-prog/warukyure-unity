using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

namespace Ad_Virtua.Survey
{
    /// <summary>
    /// Answer selected event: (Question Index, Answer Index, Answer Text)
    /// </summary>
    [Serializable]
    public class AnswerSelectedEvent : UnityEvent<int, int, string> { }

    /// <summary>
    /// Class that sets questions and answer choices to a single SurveyContent
    /// Receives SurveyQuestion data and reflects it in the UI
    /// </summary>
    public class SurveyContentDataSetter : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Text component to display the question")]
        [SerializeField] private Text questionText;

        [Header("Answer Object Generation Settings")]
        [Tooltip("Toggle prefab for answer choices (Toggle + Label)")]
        [SerializeField] private GameObject togglePrefab;

        [Tooltip("Parent Transform to place Toggles (ToggleGroup object)")]
        [SerializeField] private Transform toggleGroupContainer;

        [Header("Events")]
        public AnswerSelectedEvent OnAnswerSelected = new AnswerSelectedEvent();

        // Internal data
        private int questionIndex = -1;
        private SurveyQuestion questionData;
        private ToggleGroup toggleGroup;
        private List<Toggle> generatedToggles = new List<Toggle>();

        /// <summary>
        /// Question number
        /// </summary>
        public int QuestionIndex => questionIndex;

        /// <summary>
        /// Whether the question has been answered
        /// </summary>
        public bool IsAnswered
        {
            get
            {
                foreach (var toggle in generatedToggles)
                {
                    if (toggle != null && toggle.isOn) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Index of the selected answer
        /// </summary>
        public int SelectedAnswerIndex
        {
            get
            {
                for (int i = 0; i < generatedToggles.Count; i++)
                {
                    if (generatedToggles[i] != null && generatedToggles[i].isOn)
                    {
                        return i;
                    }
                }
                return -1;
            }
        }

        /// <summary>
        /// Text of the selected answer
        /// </summary>
        public string SelectedAnswerText
        {
            get
            {
                int index = SelectedAnswerIndex;
                if (index >= 0 && index < questionData?.answers?.Length)
                {
                    return questionData.answers[index];
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// Set question data
        /// </summary>
        /// <param name="index">Question number</param>
        /// <param name="question">Question data</param>
        public void SetQuestionData(int index, SurveyQuestion question)
        {
            if (question == null)
            {
                Debug.LogError("[SurveyContentDataSetter] Question data is null");
                return;
            }

            questionIndex = index;
            questionData = question;

            // Set question text
            SetQuestionText(question.question);

            // Generate answer toggles
            GenerateAnswerToggles(question.answers);

        }

        /// <summary>
        /// Set question text to UI
        /// </summary>
        private void SetQuestionText(string text)
        {
            if (questionText == null)
            {
                Debug.LogError("[SurveyContentDataSetter] QuestionText is not set");
                return;
            }

            questionText.text = $"Q{questionIndex + 1}. {text}";
        }

        /// <summary>
        /// Generate answer choice Toggles
        /// </summary>
        private void GenerateAnswerToggles(string[] answers)
        {
            if (answers == null || answers.Length == 0)
            {
                Debug.LogError("[SurveyContentDataSetter] Answer choices do not exist");
                return;
            }

            if (togglePrefab == null)
            {
                Debug.LogError("[SurveyContentDataSetter] Toggle prefab is not set");
                return;
            }

            if (toggleGroupContainer == null)
            {
                Debug.LogError("[SurveyContentDataSetter] ToggleGroupContainer is not set");
                return;
            }

            try
            {
                // Get or create ToggleGroup component
                toggleGroup = toggleGroupContainer.GetComponent<ToggleGroup>();
                if (toggleGroup == null)
                {
                    toggleGroup = toggleGroupContainer.gameObject.AddComponent<ToggleGroup>();
                    Debug.Log("[SurveyContentDataSetter] ToggleGroup component automatically added");
                }

                // Allow all Toggles to be deselected (allow unselected state)
                toggleGroup.allowSwitchOff = true;

                // Clear existing Toggles
                ClearToggles();

                // Generate Toggle for each answer choice
                for (int i = 0; i < answers.Length; i++)
                {
                    // Instantiate Toggle prefab (as child of ToggleGroupContainer)
                    GameObject toggleObj = Instantiate(togglePrefab, toggleGroupContainer);

                    // Get Toggle component
                    Toggle toggle = toggleObj.GetComponent<Toggle>();
                    if (toggle == null)
                    {
                        toggle = toggleObj.AddComponent<Toggle>();
                        Debug.LogWarning($"[SurveyContentDataSetter] Toggle component automatically added: Index {i}");
                    }

                    // Assign to ToggleGroup (to achieve single selection)
                    toggle.group = toggleGroup;

                    // Set label text
                    Text label = toggleObj.GetComponentInChildren<Text>();
                    if (label != null)
                    {
                        label.text = answers[i];
                    }
                    else
                    {
                        Debug.LogWarning($"[SurveyContentDataSetter] Text component not found: Index {i}");
                    }

                    // Add to list
                    generatedToggles.Add(toggle);

                    // Register Toggle change event
                    int answerIndex = i; // Copy for closure
                    toggle.onValueChanged.AddListener((isOn) =>
                    {
                        if (isOn)
                        {
                            OnToggleSelected(answerIndex);
                        }
                    });
                }

            }
            catch (Exception e)
            {
                Debug.LogError($"[SurveyContentDataSetter] Toggle object generation error: {e.Message}");
            }
        }

        /// <summary>
        /// Handler when a Toggle is selected
        /// </summary>
        private void OnToggleSelected(int answerIndex)
        {
            string answerText = questionData.answers[answerIndex];

            // Propagate event upwards
            OnAnswerSelected?.Invoke(questionIndex, answerIndex, answerText);
        }

        /// <summary>
        /// Clear existing Toggles
        /// </summary>
        private void ClearToggles()
        {
            foreach (var toggle in generatedToggles)
            {
                if (toggle != null)
                {
                    toggle.onValueChanged.RemoveAllListeners();
                    Destroy(toggle.gameObject);
                }
            }
            generatedToggles.Clear();
        }

        /// <summary>
        /// Clear answer selection
        /// </summary>
        public void ClearAnswer()
        {
            foreach (var toggle in generatedToggles)
            {
                if (toggle != null)
                {
                    toggle.isOn = false;
                }
            }
        }

        /// <summary>
        /// Get question and answer information (for debugging)
        /// </summary>
        public string GetQuestionInfo()
        {
            return $"Q{questionIndex + 1}: {questionData?.question ?? "N/A"}\n" +
                   $"Answered: {IsAnswered}\n" +
                   $"Selected: {(IsAnswered ? $"A{SelectedAnswerIndex + 1} - {SelectedAnswerText}" : "None")}";
        }

        private void OnDestroy()
        {
            // Cleanup event listeners
            foreach (var toggle in generatedToggles)
            {
                if (toggle != null)
                {
                    toggle.onValueChanged.RemoveAllListeners();
                }
            }
        }
    }
}
