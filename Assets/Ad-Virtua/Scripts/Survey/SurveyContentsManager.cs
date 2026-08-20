using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Ad_Virtua.Survey
{
    /// <summary>
    /// Manager that manages multiple SurveyContents and aggregates answer results
    /// </summary>
    public class SurveyContentsManager : MonoBehaviour
    {
        [SerializeField] private Button submitButton;
        [SerializeField] private Button closeButton;

        /// <summary>
        /// Callback when submit button is pressed (answer result JSON)
        /// </summary>
        public event Action<string> OnSurveySubmitted;

        /// <summary>
        /// Callback when close button is pressed (answer result JSON)
        /// </summary>
        public event Action<string> OnSurveyClosed;

        // Management data
        private string surveyId;
        private List<SurveyContentDataSetter> surveyContents = new List<SurveyContentDataSetter>();
        private Dictionary<int, int> answersMap = new Dictionary<int, int>(); // Question Index → Answer Index

        /// <summary>
        /// Whether all questions have been answered
        /// </summary>
        public bool AreAllQuestionsAnswered
        {
            get
            {
                if (surveyContents.Count == 0) return false;

                foreach (var content in surveyContents)
                {
                    if (!content.IsAnswered)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private void Start()
        {
            // Disable button's initial state
            UpdateButtonState();

            // Register submit button click event
            if (submitButton != null)
            {
                submitButton.onClick.AddListener(OnSubmitButtonClicked);
            }

            // Register close button click event
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
        }

        /// <summary>
        /// Set survey ID
        /// </summary>
        public void SetSurveyId(string id)
        {
            surveyId = id;
        }

        /// <summary>
        /// Register SurveyContent
        /// </summary>
        public void RegisterSurveyContent(SurveyContentDataSetter content)
        {
            if (content == null)
            {
                Debug.LogError("[SurveyContentsManager] SurveyContent to register is null");
                return;
            }

            if (!surveyContents.Contains(content))
            {
                surveyContents.Add(content);

                // Subscribe to answer change event
                content.OnAnswerSelected.AddListener(OnAnswerSelectedHandler);

            }
        }

        /// <summary>
        /// Handler when an answer is selected
        /// </summary>
        private void OnAnswerSelectedHandler(int questionIndex, int answerIndex, string answerText)
        {
            // Record answer
            answersMap[questionIndex] = answerIndex;

            // Update button state
            UpdateButtonState();
        }

        /// <summary>
        /// Handler when submit button is clicked
        /// </summary>
        private void OnSubmitButtonClicked()
        {
            if (!AreAllQuestionsAnswered)
            {
                Debug.LogWarning("[SurveyContentsManager] Not all questions have been answered");
                return;
            }

            // Get answer results in JSON format
            string resultJson = GetAnswersAsJson();

            // Fire event (notification to AdPlay)
            OnSurveySubmitted?.Invoke(resultJson);

            // Destroy own GameObject
            Destroy(gameObject);
        }

        /// <summary>
        /// Handler when close button is clicked
        /// Sends current answers (partial answers allowed) and closes survey
        /// </summary>
        private void OnCloseButtonClicked()
        {
            // Get answer results in JSON format (partial answers included)
            string resultJson = GetAnswersAsJson();

            // Fire close event (notification to AdPlay)
            OnSurveyClosed?.Invoke(resultJson);

            // Destroy own GameObject
            Destroy(gameObject);
        }

        /// <summary>
        /// Get answer results in JSON format
        /// </summary>
        public string GetAnswersAsJson()
        {
            try
            {
                // Build answer data
                List<AnswerData> answersList = new List<AnswerData>();

                foreach (var content in surveyContents)
                {
                    if (content.IsAnswered)
                    {
                        AnswerData data = new AnswerData
                        {
                            questionIndex = content.QuestionIndex,
                            answerIndex = content.SelectedAnswerIndex,
                            answerText = content.SelectedAnswerText
                        };

                        answersList.Add(data);
                    }
                }

                // Create result object
                SurveyAnswerResult result = new SurveyAnswerResult
                {
                    surveyId = surveyId,
                    answers = answersList.ToArray()
                };

                // Convert to JSON
                string json = JsonUtility.ToJson(result, true);
                return json;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SurveyContentsManager] JSON conversion error: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get answer status summary
        /// </summary>
        public string GetAnswerSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== Survey Answer Summary (ID: {surveyId}) ===");
            sb.AppendLine($"Total Questions: {surveyContents.Count}");
            sb.AppendLine($"Answered: {answersMap.Count}");
            sb.AppendLine($"All Completed: {AreAllQuestionsAnswered}");
            sb.AppendLine();

            foreach (var content in surveyContents)
            {
                sb.AppendLine($"Q{content.QuestionIndex + 1}: {(content.IsAnswered ? "✓" : "✗")} " +
                             $"{(content.IsAnswered ? $"→ {content.SelectedAnswerText}" : "")}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Clear all answers
        /// </summary>
        public void ClearAllAnswers()
        {
            answersMap.Clear();

            foreach (var content in surveyContents)
            {
                content.ClearAnswer();
            }

            // Update button state
            UpdateButtonState();

            Debug.Log("[SurveyContentsManager] All answers have been cleared");
        }

        /// <summary>
        /// Reset management data
        /// </summary>
        public void ResetManager()
        {
            // Remove event listeners
            foreach (var content in surveyContents)
            {
                if (content != null)
                {
                    content.OnAnswerSelected.RemoveListener(OnAnswerSelectedHandler);
                }
            }

            surveyContents.Clear();
            answersMap.Clear();
            surveyId = string.Empty;
        }

        /// <summary>
        /// Update button enabled/disabled state
        /// </summary>
        private void UpdateButtonState()
        {
            if (submitButton != null)
            {
                submitButton.interactable = AreAllQuestionsAnswered;
            }
        }

        private void OnDestroy()
        {
            // Remove button events
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(OnSubmitButtonClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }

            // Cleanup
            ResetManager();
        }
    }
}
