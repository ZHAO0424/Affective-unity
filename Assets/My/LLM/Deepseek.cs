using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;
using TMPro;
using Meta.WitAi.TTS.Integrations;
using Meta.WitAi.TTS.Utilities;
using Newtonsoft.Json;

public class Deepseek : MonoBehaviour
{
    [Header("UI Components")]
    public TMP_InputField userInput;
    public Button sendButton;
    public TMP_Text outputText;
    public TMP_Text statusText;
    public TMP_Text speechRecognitionText;
    public Button processRecognizedTextButton;

    [Header("API Settings")]
    public string apiUrl = "http://localhost:11434/api/generate";

    [Header("Model Settings")]
    public string modelName = "deepseek-r1:7b";
    public int maxTokens = 80;
    [Range(0.1f, 1.5f)] public float temperature = 0.85f;

    [Header("GPU Settings")]
    [Range(0, 40)] public int numGpuLayers = 20;

    [Header("TTS Settings")]
    public TTSSpeaker ttsSpeaker;

    private class OllamaRequest
    {
        public string model;
        public string prompt;
        public bool stream = false;
        public int max_tokens;
        public float temperature;
        public int num_gpu_layers;
    }

    private class OllamaResponse
    {
        public string response;
        public bool done;
    }

    void Start()
    {
        sendButton.onClick.AddListener(OnSendMessage);
        userInput.onEndEdit.AddListener(OnInputEndEdit);

        if (processRecognizedTextButton != null)
        {
            processRecognizedTextButton.onClick.AddListener(ProcessRecognizedText);
        }

        UpdateStatus($"[{DateTime.Now:HH:mm:ss}] System Ready (Model: {modelName})");
    }

    private void OnInputEndEdit(string _)
    {
        if (Input.GetKeyDown(KeyCode.Return)) OnSendMessage();
    }

    public void OnSendMessage()
    {
        if (!string.IsNullOrEmpty(userInput.text))
        {
            StartCoroutine(SendRequest(userInput.text));
            AppendText($"Q: {userInput.text}\n");
            userInput.text = "";
            sendButton.interactable = false;
        }
    }

    public void ProcessRecognizedText()
    {
        if (!string.IsNullOrEmpty(speechRecognitionText.text))
        {
            StartCoroutine(SendRequest(speechRecognitionText.text));
            AppendText($"Q: {speechRecognitionText.text}\n");
            sendButton.interactable = false;
        }
    }

    public IEnumerator SendRequest(string message)
    {
        UpdateStatus($"[{DateTime.Now:HH:mm:ss}] Processing...");
        float startTime = Time.time;

        string systemPrompt = @"You are a helpful and empathetic assistant.
             Respond briefly and kindly in one or two short sentences. 
             Avoid formal tones. Keep it friendly and human.";

        OllamaRequest requestData = new OllamaRequest
        {
            model = modelName,
            prompt = systemPrompt + message + " [/INST]",
            stream = false,
            max_tokens = maxTokens,
            temperature = temperature,
            num_gpu_layers = numGpuLayers
        };

        string jsonData = JsonConvert.SerializeObject(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using UnityWebRequest request = new UnityWebRequest(apiUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(bodyRaw),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 30
        };

        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();

        try
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                HandleResponse(request.downloadHandler.text);
            }
            else
            {
                HandleApiError(request);
            }
        }
        finally
        {
            sendButton.interactable = true;
        }
    }

    private void HandleResponse(string responseText)
    {
        try
        {
            OllamaResponse response = JsonConvert.DeserializeObject<OllamaResponse>(responseText);
            if (response.done && !string.IsNullOrEmpty(response.response))
            {
                string processedResponse = PostProcessResponse(response.response);
                AppendText($"Counselor: {processedResponse}\n");
                
                ttsSpeaker.Speak(processedResponse);
                UpdateStatus($"[{DateTime.Now:HH:mm:ss}] Response Ready");
            }
        }
        catch (Exception e)
        {
            UpdateStatus($"[{DateTime.Now:HH:mm:ss}] Parsing error: {e.Message}");
        }
    }

    private string PostProcessResponse(string rawResponse)
    {
        // Get only the final response by looking for common patterns
        string processedResponse = rawResponse;

        // Try to identify the final answer portion
        // Common pattern: The model thinks, then gives a final answer

        // Strategy 1: Find the last paragraph after clearing all the thinking sections
        string[] paragraphs = processedResponse.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (paragraphs.Length > 1)
        {
            // Take only the last paragraph which is likely the final answer
            processedResponse = paragraphs[paragraphs.Length - 1];
        }

        // Strategy 2: Look for specific markers that often precede the final answer
        string[] finalAnswerMarkers = new[] {
        "Final answer:",
        "My response:",
        "Response to client:",
        "So my response is:",
        "As a counselor, I would respond:",
        "My response to the client:"
    };

        foreach (var marker in finalAnswerMarkers)
        {
            int markerIndex = processedResponse.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                processedResponse = processedResponse.Substring(markerIndex + marker.Length);
                break;
            }
        }

        // Clean up any remaining formatting
        return processedResponse
            .Replace("**", "")
            .Replace("Counselor:", "")
            .Replace("\n", " ")
            .Replace("```", "")  // Remove code blocks if present
            .Trim();
    }

    private void HandleApiError(UnityWebRequest request)
    {
        UpdateStatus($"[{DateTime.Now:HH:mm:ss}] Service error: {request.error}");
    }

    void AppendText(string text)
    {
        if (outputText != null)
        {
            outputText.text += text;
            var scrollRect = outputText.GetComponentInParent<ScrollRect>();
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0;
        }
    }

    void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}