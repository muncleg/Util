#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class ObjectPoolDebugWindow : EditorWindow
{
    private bool showDetails = false; // 상세정보 Foldout 제어

    [MenuItem("Tools/Object Pool Debug")]
    public static void ShowWindow()
    {
        GetWindow<ObjectPoolDebugWindow>("Object Pool Debug");
    }

    private void OnInspectorUpdate()
    {
        // 자동 갱신
        Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            GUILayout.Label("플레이 모드에서만 사용 가능합니다.", EditorStyles.boldLabel);
            return;
        }

        var manager = ObjectPoolManager.Instance;
        if (manager == null)
        {
            GUILayout.Label("ObjectPoolManager 인스턴스가 없음.", EditorStyles.boldLabel);
            return;
        }

        GUILayout.Label("현재 풀 상태:", EditorStyles.boldLabel);
        var info = manager.GetDetailedPoolInfo();
        if (info.Count == 0)
        {
            GUILayout.Label("풀에 아무것도 없습니다.");
        }
        else
        {
            foreach (var kvp in info)
            {
                GUILayout.Label($"풀 키: {kvp.Key}", EditorStyles.boldLabel);
                GUILayout.Label($"오브젝트 수: {kvp.Value.Count}개");
                GUILayout.Space(10); // 풀 사이에 여백
            }
        }

        GUILayout.Space(10);
        showDetails = EditorGUILayout.Foldout(showDetails, "Show Detailed Info");
        if (showDetails)
        {
            var detailedInfo = manager.GetDetailedPoolInfo();
            foreach (var kvp in detailedInfo)
            {
                string key = kvp.Key;
                var list = kvp.Value;

                EditorGUILayout.LabelField(key, EditorStyles.boldLabel);
                // 테이블 헤더
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("ObjName", GUILayout.Width(100));
                EditorGUILayout.LabelField("TimeInPool(s)", GUILayout.Width(100));
                EditorGUILayout.LabelField("TimeUntilCleanup(s)", GUILayout.Width(130));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < list.Count; i++)
                {
                    var infoItem = list[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(infoItem.objName, GUILayout.Width(100));
                    EditorGUILayout.LabelField(infoItem.timeInPool.ToString("F2"), GUILayout.Width(100));
                    EditorGUILayout.LabelField(infoItem.timeUntilCleanup.ToString("F2"), GUILayout.Width(130));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space();
            }
        }

        // EditorGUILayout.Space();
        // if (GUILayout.Button("Clear All Pools"))
        // {
        //     //  manager.ClearAll();
        //     Debug.Log("[ObjectPoolDebugWindow] All pools cleared.");
        // }
    }
}
#endif
