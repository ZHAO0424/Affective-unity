using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private readonly Queue<System.Action> _actions = new Queue<System.Action>();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<UnityMainThreadDispatcher>();
                if (_instance == null)
                {
                    var go = new GameObject("MainThreadDispatcher");
                    _instance = go.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public void Enqueue(System.Action action)
    {
        lock (_actions)
        {
            _actions.Enqueue(action);
        }
    }

    void Update()
    {
        lock (_actions)
        {
            while (_actions.Count > 0)
            {
                _actions.Dequeue()?.Invoke();
            }
        }
    }
}
