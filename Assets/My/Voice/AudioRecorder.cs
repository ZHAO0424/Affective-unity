using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;

public class AudioRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    [Tooltip("录音时长（秒）")]
    public int recordDuration = 5;

    [Tooltip("输出文件名（.wav）")]
    public string outputFileName = "user_input.wav";

    private AudioClip recordedClip;

    private void Update()
    {
        // 使用新输入系统检测按键
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartRecording();
        }

        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            StopRecordingAndSave();
        }
    }

    public void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("未检测到可用麦克风设备。");
            return;
        }

        // 开始录音
        recordedClip = Microphone.Start(null, false, recordDuration, 16000);
        Debug.Log("开始录音...");
    }

    public void StopRecordingAndSave()
    {
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
            Debug.Log("录音结束，开始保存");

            SaveWavFile(recordedClip);
        }
        else
        {
            Debug.LogWarning("当前没有正在进行的录音。");
        }
    }

    private void SaveWavFile(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("保存失败：录音剪辑为空。");
            return;
        }

        string savePath = Path.Combine(Application.persistentDataPath, outputFileName);
        byte[] wavData = WavUtility.FromAudioClip(clip, out string temp, true);
        File.WriteAllBytes(savePath, wavData);
        Debug.Log($"保存为 WAV 成功，路径：{savePath}");
    }
}