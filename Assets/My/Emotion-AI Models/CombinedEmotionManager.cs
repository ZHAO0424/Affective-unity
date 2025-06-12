using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class CombinedEmotionManager : MonoBehaviour
{
    [Header("组件引用")]
    public Speech2 speechRecognizer;
    public Deepseek deepseekAI; // 拖入 Deepseek 脚本引用
    public TMP_Text combinedResultText;
    public Deepseek deepseekManager;
    void OnEnable()
    {
        if (speechRecognizer == null)
        {
            speechRecognizer = FindObjectOfType<Speech2>();
            if (speechRecognizer == null)
            {
                Debug.LogError("CombinedEmotionManager: Speech2 component not found!");
                enabled = false;
                return;
            }
        }

        speechRecognizer.OnSynchronizedEmotionRecognized += HandleSynchronizedEmotion;
    }

    void OnDisable()
    {
        if (speechRecognizer != null)
        {
            speechRecognizer.OnSynchronizedEmotionRecognized -= HandleSynchronizedEmotion;
        }
    }

    void HandleSynchronizedEmotion(SynchronizedEmotionResult result)
    {
        // 显示到 UI 上
        if (combinedResultText != null)
        {
            combinedResultText.text = $"Recognized content: \"{result.UtteranceText}\"\n" +
                                      $"Text emotion: {result.TextEmotion} (Confidence: {result.TextEmotionScore:F2})\n" +
                                      $"Voice emotion: {result.AudioEmotion} (Confidence: {result.AudioEmotionScore:F2})";
        }

        // 构建给 AI 的 Prompt
        string prompt = 
            $"A user said: \"{result.UtteranceText}\".\n" +
            $"Their speech tone suggests: {result.AudioEmotion} (confidence: {result.AudioEmotionScore:F2}).\n" +
            $"The textual content suggests: {result.TextEmotion} (confidence: {result.TextEmotionScore:P2}).\n\n" +
            $"Based on this, please respond as a compassionate and emotionally intelligent assistant.\n" +
            $"Acknowledge both what was said and how they might be feeling.\n" +
            $"Keep your response supportive, brief, and understanding.";

        // 发送给 AI 模型
        Debug.Log($"[Emotion→Deepseek] Text: {result.UtteranceText} | TextEmotion: {result.TextEmotion} | AudioEmotion: {result.AudioEmotion}");

        if (deepseekAI != null)
        {
            StartCoroutine(deepseekAI.SendRequest(prompt));
        }
        else
        {
            Debug.LogError("Deepseek AI reference not set!");
        }
    }
}



