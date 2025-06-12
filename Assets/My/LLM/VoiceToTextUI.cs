using UnityEngine;
using TMPro;
using Oculus.Voice; // 引入 Meta Voice SDK
using Meta.WitAi.Json;
using System.Collections;

public class VoiceToTextUI : MonoBehaviour
{
    public AppVoiceExperience voiceExperience; // 语音识别组件
    public TMP_Text resultText; // 识别结果显示的文本框

    void Start()
    {
        StartCoroutine(InitializeVoiceService());
    }

    IEnumerator InitializeVoiceService()
    {
        yield return new WaitForSeconds(1f); // 给其他组件时间初始化

        if (voiceExperience == null)
        {
            Debug.LogError("❌ AppVoiceExperience 未绑定！");
            yield break;
        }
        // 绑定事件
        voiceExperience.VoiceEvents.OnResponse.AddListener(OnVoiceResponse);
        voiceExperience.VoiceEvents.OnError.AddListener(OnError);
        voiceExperience.VoiceEvents.OnStartListening.AddListener(OnListeningStart);
        voiceExperience.VoiceEvents.OnStoppedListening.AddListener(OnListeningStop);
        Debug.Log("✅ VoiceToTextUI 已初始化成功！");
    }

    void OnListeningStart()
    {
        Debug.Log("🎤 开始监听...");
        resultText.text = "Listening...";
    }

    void OnListeningStop()
    {
        Debug.Log("⏳ 停止监听，正在处理...");
        resultText.text = "Processing...";
    }

    void OnVoiceResponse(WitResponseNode response)
    {
        if (response == null)
        {
            Debug.LogError("❌ OnVoiceResponse: 服务器未返回任何数据！");
            resultText.text = "No response!";
            return;
        }

        // Debug 打印完整的 Wit.ai 响应
        Debug.Log($"📝 Wit.ai 响应: {response.ToString()}");

        // 提取语音识别文本
        string transcript = response["text"];
        if (!string.IsNullOrEmpty(transcript))
        {
            Debug.Log($"✅ 语音转文本成功: {transcript}");
            resultText.text = transcript;
        }
        else
        {
            Debug.LogWarning("⚠️ 语音未能识别！");
            resultText.text = "Could not understand!";
        }
    }

    void OnError(string error, string message)
    {
        Debug.LogError($"❌ 语音识别错误: {error} - {message}");
        resultText.text = $"Error: {message}";
    }

    private void OnDestroy()
    {
        // 移除监听
        if (voiceExperience != null)
        {
            voiceExperience.VoiceEvents.OnResponse.RemoveListener(OnVoiceResponse);
            voiceExperience.VoiceEvents.OnError.RemoveListener(OnError);
            voiceExperience.VoiceEvents.OnStartListening.RemoveListener(OnListeningStart);
            voiceExperience.VoiceEvents.OnStoppedListening.RemoveListener(OnListeningStop);
        }

        Debug.Log("🛑 VoiceToTextUI 组件已销毁！");
    }

    public void StartListening()
    {
        if (!Microphone.IsRecording(null))  // 检查麦克风是否可用
        {
            Debug.Log("🎤 麦克风可用，开始语音捕捉...");
        }
        else
        {
            Debug.LogWarning("⚠️ 麦克风已在录音！");
        }

        if (voiceExperience != null)
        {
            Debug.Log("▶️ 触发语音识别...");
            voiceExperience.Activate();
            resultText.text = "Listening...";
        }
        else
        {
            Debug.LogError("❌ voiceExperience 未绑定！");
        }
    }
}


