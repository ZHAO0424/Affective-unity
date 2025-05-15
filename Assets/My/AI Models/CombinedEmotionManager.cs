// CombinedEmotionManager.cs

using UnityEngine;
using UnityEngine.UI;

public class CombinedEmotionManager : MonoBehaviour
{
    [Header("组件引用 (可在 Inspector 里拖)")]
    // public RealTimeEmotionRecognizer audioRecognizer; // 不再需要直接引用
    // public EmotionRecognizerSentis textRecognizer;   // 不再需要直接引用
    public Speech2 speechRecognizer;

    [Header("显示 UI")]
    public Text combinedResultText; // 用于显示结果的UI Text组件

    void OnEnable()
    {
        if (speechRecognizer == null)
        {
            speechRecognizer = FindObjectOfType<Speech2>();
            if (speechRecognizer == null)
            {
                Debug.LogError("CombinedEmotionManager: Speech2 component not found in the scene!");
                enabled = false; // Disable this component if Speech2 is missing
                return;
            }
        }

        // 订阅 Speech2 的新事件
        speechRecognizer.OnSynchronizedEmotionRecognized += HandleSynchronizedEmotion;
    }

    void OnDisable()
    {
        if (speechRecognizer != null)
        {
            // 取消订阅
            speechRecognizer.OnSynchronizedEmotionRecognized -= HandleSynchronizedEmotion;
        }
    }

    // 处理来自 Speech2 的同步情绪结果
    void HandleSynchronizedEmotion(SynchronizedEmotionResult result)
    {
        // 更新UI显示
        if (combinedResultText != null)
        {
            combinedResultText.text = $"文本: \"{result.UtteranceText}\"\n" +
                                      $"✍️ 文本情绪: {result.TextEmotion} (Score: {result.TextEmotionScore:P2})\n" +
                                      $"🔊 语音情绪: {result.AudioEmotion} (Score: {result.AudioEmotionScore:F2})";
        }

        // 你也可以在这里进行其他逻辑处理，比如根据情绪结果改变角色行为等
        Debug.Log($"[CombinedManager] Received Synced Emotion: Text='{result.TextEmotion}' ({result.TextEmotionScore:P2}), Audio='{result.AudioEmotion}' ({result.AudioEmotionScore:F2}) for '{result.UtteranceText}'");
    }

    // 原有的 HandleAudio 和 HandleText 以及 UpdateDisplay 可以移除了
    // private string lastAudioEmotion = "—";
    // private string lastTextEmotion = "—";
    // void HandleAudio(string emo) { /* ... */ }
    // void HandleText(string senti) { /* ... */ }
    // void UpdateDisplay() { /* ... */ }
}


