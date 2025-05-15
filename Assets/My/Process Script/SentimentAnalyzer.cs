using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using UnityEngine.InputSystem; // 保留，如果还需要空格测试
using SimpleJSON; // 确保你正确导入了 SimpleJSON 命名空间

public class SentimentAnalyzer : MonoBehaviour
{
    // 保持你的 Endpoint 和 Key
    public string endpoint = "https://zzr-emotion.cognitiveservices.azure.com/text/analytics/v3.1/sentiment"; // 建议使用 v3.1，但v3.0应该也能工作
    public string key = "9Wrtp6XioyLyqpaRwOZvc4xdQhEByQz0Yy1ZZ5m2la7KBMJphvxiJQQJ99BDACYeBjFXJ3w3AAAEACOGo85n"; // 请替换为你自己的 Key

    public void AnalyzeSentiment(string text)
    {
        // 添加输入检查
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("AnalyzeSentiment 接收到空文本，已跳过。");
            return;
        }
        Debug.Log($"🎯 即将分析情绪，文本: '{text}'");
        StartCoroutine(SendSentimentRequest(text));
    }

    IEnumerator SendSentimentRequest(string text)
    {
        // 使用 v3.1 的 JSON 结构 (和 v3.0 基本兼容，但 language 改为 languageCode)
        // 如果坚持用 v3.0，保持 "language": "zh"
        string jsonBody = $"{{\"documents\": [{{\"id\": \"1\", \"language\": \"en\", \"text\": \"{EscapeJsonString(text)}\"}}]}}";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        // 使用 using 语句确保 UnityWebRequest 被正确 Dispose
        using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Ocp-Apim-Subscription-Key", key);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json"); // 最好也加上 Accept 头

            yield return request.SendWebRequest();

            // 检查网络错误或 HTTP 错误
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"情绪分析请求失败: {request.error}");
                Debug.LogError($"错误详情: {request.downloadHandler?.text}"); // 显示返回的错误信息
            }
            else if (request.result == UnityWebRequest.Result.Success)
            {
                string responseJson = request.downloadHandler.text;
                Debug.Log("情绪分析完整JSON响应: " + responseJson);
                try
                {
                    // 使用 SimpleJSON 解析
                    JSONNode json = JSON.Parse(responseJson);

                    // 健壮性检查：确保路径存在
                    if (json != null && json["documents"] != null && json["documents"][0] != null && json["documents"][0]["sentiment"] != null)
                    {
                        string sentiment = json["documents"][0]["sentiment"];
                        Debug.Log("文本: " + text);
                        Debug.Log("情绪分析结果: " + sentiment);

                        // 在这里可以根据 sentiment 做后续处理
                        // FindObjectOfType<AIResponseGenerator>()?.GenerateResponse(text, sentiment);
                    }
                    else
                    {
                        Debug.LogError("情绪分析响应JSON结构不符合预期。");
                        if (json != null && json["error"] != null)
                        {
                            Debug.LogError($"API 返回错误: Code={json["error"]["code"]}, Message={json["error"]["message"]}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"解析情绪分析JSON时出错: {ex.Message}\nJSON: {responseJson}");
                }
            }
            else
            {
                Debug.LogError("情绪分析请求遇到未知错误。 Result: " + request.result);
            }
        } // using 语句结束，request 会自动 Dispose
    }

    // 辅助函数，用于转义 JSON 字符串中的特殊字符
    private string EscapeJsonString(string str)
    {
        if (str == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (char c in str)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        // 控制字符转换为 \uXXXX 格式
                        sb.AppendFormat("\\u{0:x4}", (int)c);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }


    // 保留 Update 用于空格键测试（如果需要）
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("空格键测试触发");
            AnalyzeSentiment("今天天气真好，心情非常愉快！"); // 用一个更积极的句子测试
        }
    }
}
