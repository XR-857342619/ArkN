using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;



public class logdata
{
    public string output = "";
    public string stack = "";

    public static logdata Init(string o, string s)
    {
        return new logdata
        {
            output = o,
            stack = s
        };
    }

    public void Show()
    {
        GUILayout.Label(output);
        GUILayout.Label(stack);
    }
}

/// <summary>
/// 手机调试脚本
/// 本脚本挂在一个空对象或转换场景时不删除的对象便可
/// 错误和异常输出日记路径 Application.persistentDataPath/outLog.txt
/// </summary>
public class ShowDebugInPhone : MonoBehaviour
{
#if UNITY_ANDROID
    const int MaxLogCount = 300;

    readonly List<logdata> logDatas = new List<logdata>();
    readonly List<logdata> errorDatas = new List<logdata>();
    readonly List<logdata> warningDatas = new List<logdata>();
    readonly Queue<string> writeQueue = new Queue<string>();
    readonly object logLock = new object();

    Vector2 uiLog;
    Vector2 uiError;
    Vector2 uiWarning;
    bool open = false;
    bool showLog = false;
    bool showError = false;
    bool showWarning = false;

    string outpath;
    StreamWriter writer;

    void Start()
    {
        // Application.persistentDataPath 是运行时唯一既可读又可写的路径。
        outpath = Path.Combine(Application.persistentDataPath, "outLog.txt");

        // 每次启动客户端删除以前保存的 Log。
        if (File.Exists(outpath))
        {
            File.Delete(outpath);
        }

        writer = new StreamWriter(outpath, true, new UTF8Encoding(false))
        {
            AutoFlush = true
        };

        // 转换场景不删除。
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        // 推荐使用 logMessageReceivedThreaded 替代已过时的 RegisterLogCallback。
        Application.logMessageReceivedThreaded += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceivedThreaded -= HandleLog;
    }

    void OnDestroy()
    {
        if (writer != null)
        {
            writer.Dispose();
            writer = null;
        }
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        logdata data = logdata.Init(logString, stackTrace);

        // logMessageReceivedThreaded 可能来自非主线程，需要加锁保护集合。
        lock (logLock)
        {
            switch (type)
            {
                case LogType.Log:
                    AddLog(logDatas, data);
                    break;

                case LogType.Error:
                case LogType.Exception:
                    AddLog(errorDatas, data);
                    writeQueue.Enqueue(logString);
                    writeQueue.Enqueue(stackTrace);
                    break;

                case LogType.Warning:
                    AddLog(warningDatas, data);
                    break;
            }
        }
    }

    static void AddLog(List<logdata> list, logdata data)
    {
        list.Add(data);
        if (list.Count > MaxLogCount)
        {
            list.RemoveRange(0, list.Count - MaxLogCount);
        }
    }

    void Update()
    {
        if (writer == null) return;

        // 写文件统一放在主线程 Update 中，避免多线程同时写文件。
        lock (logLock)
        {
            while (writeQueue.Count > 0)
            {
                writer.WriteLine(writeQueue.Dequeue());
            }
        }
    }

    void OnGUI()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(">>Open", GUILayout.Height(150), GUILayout.Width(150)))
        {
            open = !open;
        }

        if (open)
        {
            if (GUILayout.Button("清理", GUILayout.Height(150), GUILayout.Width(150)))
            {
                lock (logLock)
                {
                    logDatas.Clear();
                    errorDatas.Clear();
                    warningDatas.Clear();
                }
            }

            if (GUILayout.Button("显示log日志:" + showLog, GUILayout.Height(150), GUILayout.Width(200)))
            {
                showLog = !showLog;
                if (open)
                    open = !open;
            }

            if (GUILayout.Button("显示error日志:" + showError, GUILayout.Height(150), GUILayout.Width(200)))
            {
                showError = !showError;
                if (open)
                    open = !open;
            }

            if (GUILayout.Button("显示warning日志:" + showWarning, GUILayout.Height(150), GUILayout.Width(200)))
            {
                showWarning = !showWarning;
                if (open)
                    open = !open;
            }
        }

        GUILayout.EndHorizontal();

        if (showLog)
        {
            logdata[] logs;
            lock (logLock)
            {
                logs = logDatas.ToArray();
            }

            GUI.color = Color.white;
            uiLog = GUILayout.BeginScrollView(uiLog);
            foreach (var va in logs)
            {
                va.Show();
            }
            GUILayout.EndScrollView();
        }

        if (showError)
        {
            logdata[] errors;
            lock (logLock)
            {
                errors = errorDatas.ToArray();
            }

            GUI.color = Color.red;
            uiError = GUILayout.BeginScrollView(uiError);
            foreach (var va in errors)
            {
                va.Show();
            }
            GUILayout.EndScrollView();
        }

        if (showWarning)
        {
            logdata[] warnings;
            lock (logLock)
            {
                warnings = warningDatas.ToArray();
            }

            GUI.color = Color.yellow;
            uiWarning = GUILayout.BeginScrollView(uiWarning);
            foreach (var va in warnings)
            {
                va.Show();
            }
            GUILayout.EndScrollView();
        }
    }
#endif
}
