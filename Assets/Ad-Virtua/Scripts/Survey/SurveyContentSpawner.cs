using UnityEngine;
using System;

namespace Ad_Virtua.Survey
{
    /// <summary>
    /// Spawner that generates multiple SurveyContents on Canvas
    /// Receives survey data from ResponseURL and generates SurveyContent prefab for each question
    /// </summary>
    public class SurveyContentSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject surveyContentPrefab;
        [SerializeField] private Transform contentParent;

        private SurveyContentsManager contentsManager;

        void Awake()
        {
            contentsManager = GetComponent<SurveyContentsManager>();
        }

        /// <summary>
        /// Receive survey data and generate SurveyContents
        /// </summary>
        /// <param name="surveyData">Survey data obtained from ResponseURL</param>
        public void SpawnSurveyContents(SurveyData surveyData)
        {
            if (surveyData == null || surveyData.data == null)
            {
                Debug.LogError("[SurveyContentSpawner] SurveyData is null or data does not exist");
                return;
            }

            if (surveyContentPrefab == null)
            {
                Debug.LogError("[SurveyContentSpawner] SurveyContent prefab is not set");
                return;
            }

            if (contentParent == null)
            {
                Debug.LogError("[SurveyContentSpawner] ContentParent is not set");
                return;
            }

            if (contentsManager == null)
            {
                Debug.LogError("[SurveyContentSpawner] ContentsManager is not set");
                return;
            }

            // Clear existing SurveyContents
            ClearExistingContents();

            try
            {
                // Set survey ID
                contentsManager.SetSurveyId(surveyData.id);

                // Generate SurveyContent for each question
                for (int i = 0; i < surveyData.data.Length; i++)
                {
                    SurveyQuestion question = surveyData.data[i];

                    // Instantiate SurveyContent prefab
                    GameObject contentObj = Instantiate(surveyContentPrefab, contentParent);

                    // Get SurveyContentDataSetter
                    SurveyContentDataSetter dataSetter = contentObj.GetComponent<SurveyContentDataSetter>();

                    if (dataSetter == null)
                    {
                        Destroy(contentObj);
                        continue;
                    }

                    // Set question index and data
                    dataSetter.SetQuestionData(i, question);

                    // Register to Manager
                    contentsManager.RegisterSurveyContent(dataSetter);
                }

            }
            catch (Exception e)
            {
                Debug.LogError($"[SurveyContentSpawner] Error during SurveyContent generation: {e.Message}");
            }
        }

        /// <summary>
        /// Delete all existing SurveyContents
        /// </summary>
        private void ClearExistingContents()
        {
            if (contentParent == null) return;

            // Delete all child objects under contentParent
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                Destroy(contentParent.GetChild(i).gameObject);
            }

        }

        /// <summary>
        /// Initialize: Set reference to Manager
        /// </summary>
        /// <param name="manager">Reference to SurveyContentsManager</param>
        public void Initialize(SurveyContentsManager manager)
        {
            contentsManager = manager;
        }

        /// <summary>
        /// Set ContentParent dynamically
        /// </summary>
        public void SetContentParent(Transform parent)
        {
            contentParent = parent;
        }
    }
}
