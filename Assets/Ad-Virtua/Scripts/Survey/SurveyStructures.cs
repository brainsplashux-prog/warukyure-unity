using System;
using UnityEngine.Scripting;

namespace Ad_Virtua.Survey
{
    /// <summary>
    /// Survey question data
    /// Manages one question and multiple answer choices
    /// IL2CPPストリッピング対策として[Preserve]属性を付与
    /// </summary>
    [Serializable]
    [Preserve]
    public class SurveyQuestion
    {
        [Preserve] public string question;    // Question text
        [Preserve] public string[] answers;   // Answer choices array
    }

    /// <summary>
    /// Entire survey data
    /// Manages survey ID and question data array
    /// IL2CPPストリッピング対策として[Preserve]属性を付与
    /// </summary>
    [Serializable]
    [Preserve]
    public class SurveyData
    {
        [Preserve] public string id;                  // Survey ID
        [Preserve] public string campaign_id;         // Campaign ID
        [Preserve] public SurveyQuestion[] data;      // Question data array
    }

    /// <summary>
    /// Survey answer result data (for JSON output)
    /// IL2CPPストリッピング対策として[Preserve]属性を付与
    /// </summary>
    [Serializable]
    [Preserve]
    public class SurveyAnswerResult
    {
        [Preserve] public string surveyId;          // Survey ID
        [Preserve] public AnswerData[] answers;     // Answer data for each question
    }

    /// <summary>
    /// Answer data for each question
    /// IL2CPPストリッピング対策として[Preserve]属性を付与
    /// </summary>
    [Serializable]
    [Preserve]
    public class AnswerData
    {
        [Preserve] public int questionIndex;        // Question number
        [Preserve] public int answerIndex;          // Index of selected answer
        [Preserve] public string answerText;        // Text of selected answer
    }
}
