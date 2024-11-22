using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class AnimationFromFolderSelection : EditorWindow
{
    private string animationState = "walk";
    private string frameRate = "12";
    private DefaultAsset selectedFolder;

    [MenuItem("munclegEditor/Auto Animation From Folder Selection")]
    public static void ShowWindow()
    {
        GetWindow<AnimationFromFolderSelection>(false, "Auto Animation From Folder Selection");
    }

    private void OnGUI()
    {
        GUILayout.Label("단일 폴더 선택", EditorStyles.boldLabel);
        selectedFolder = EditorGUILayout.ObjectField("폴더 선택", selectedFolder, typeof(DefaultAsset), false) as DefaultAsset;

        animationState = EditorGUILayout.TextField("애니메이션 상태", animationState);
        frameRate = EditorGUILayout.TextField("프레임 레이트", frameRate);

        if (GUILayout.Button("선택한 폴더로부터 애니메이션 생성") && selectedFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(selectedFolder);
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                ProcessFolder(folderPath);
            }
            else
            {
                Debug.LogError("선택한 자산이 폴더가 아닙니다.");
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("모든 'Enemy XX' 하위 폴더에 대한 일괄 처리", EditorStyles.boldLabel);

        if (GUILayout.Button("모든 'Enemy XX' 하위 폴더 처리") && selectedFolder != null)
        {
            string rootFolderPath = AssetDatabase.GetAssetPath(selectedFolder);
            if (AssetDatabase.IsValidFolder(rootFolderPath))
            {
                ProcessAllEnemyFolders(rootFolderPath);
            }
            else
            {
                Debug.LogError("선택한 자산이 폴더가 아닙니다.");
            }
        }
    }

    private void ProcessFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"폴더가 존재하지 않습니다: {folderPath}");
            return;
        }

        CreateAnimationsAndPrefab(folderPath);
    }

    private void ProcessAllEnemyFolders(string rootPath)
    {
        string[] enemyFolders = Directory.GetDirectories(rootPath, "Enemy *", SearchOption.TopDirectoryOnly);

        if (enemyFolders.Length == 0)
        {
            Debug.LogError("선택한 폴더 내에 'Enemy XX' 폴더가 없습니다.");
            return;
        }

        foreach (string enemyFolder in enemyFolders)
        {
            ProcessFolder(enemyFolder);
        }
    }

    private void CreateAnimationsAndPrefab(string path)
    {
        Dictionary<string, AnimationClip> animations = CreateAnimationClips(path);

        AnimatorOverrideController overrideController = CreateAnimatorOverrideController(path, animations);

        CreateOrUpdatePrefab(path, overrideController, animations);
    }

    private Dictionary<string, AnimationClip> CreateAnimationClips(string path)
    {
        var animations = new Dictionary<string, AnimationClip>();
        string[] spriteGUIDs = AssetDatabase.FindAssets("t:Sprite", new[] { path });
        var sprites = new List<Sprite>();

        var patterns = new Dictionary<string, string>
        {
            { "attack", @"enemy_\d+_attack_\d+\.png" },
            { "idle", @"enemy_\d+_idle_\d+\.png" },
            { "walk", @"enemy_\d+_walk_\d+\.png" }
        };

        foreach (var pattern in patterns)
        {
            Regex regex = new Regex(pattern.Value, RegexOptions.IgnoreCase);
            sprites.Clear();

            foreach (string guid in spriteGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(assetPath);

                if (regex.IsMatch(fileName))
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite != null)
                    {
                        sprites.Add(sprite);
                    }
                }
            }

            if (sprites.Count > 0)
            {
                AnimationClip clip = CreateAnimationClip(sprites, pattern.Key);
                string clipPath = Path.Combine(path, $"{pattern.Key}.anim");
                AssetDatabase.CreateAsset(clip, clipPath);
                animations[pattern.Key] = clip;
            }
        }

        return animations;
    }

    private AnimationClip CreateAnimationClip(List<Sprite> sprites, string animationName)
    {
        AnimationClip clip = new AnimationClip();
        EditorCurveBinding spriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];

        float frameRateFloat = float.TryParse(frameRate, out float result) ? result : 12f;
        clip.frameRate = frameRateFloat;
        float frameTime = 1.0f / clip.frameRate;

        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * frameTime,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

        if (animationName == "walk" || animationName == "idle")
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        return clip;
    }

    private AnimatorOverrideController CreateAnimatorOverrideController(string path, Dictionary<string, AnimationClip> animations)
    {
        string folderName = new DirectoryInfo(path).Name.Replace(" ", "");
        string overrideControllerPath = Path.Combine(path, $"{folderName}Controller.overrideController");

        AnimatorOverrideController overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overrideControllerPath);
        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController
            {
                runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/1. ArtResource/3. BaseAnicontroller/BaseAniController.controller")
            };
            AssetDatabase.CreateAsset(overrideController, overrideControllerPath);
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            var originalClip = overrides[i].Key;
            string clipName = originalClip.name.ToLower();

            if (clipName.StartsWith("attack"))
            {
                if (animations.ContainsKey("attack"))
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, animations["attack"]);
                }
                else if (animations.ContainsKey("idle"))
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, animations["idle"]);
                }
            }
            else if (clipName == "idle" && animations.ContainsKey("idle"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, animations["idle"]);
            }
            else if (clipName == "run")
            {
                if (animations.ContainsKey("walk"))
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, animations["walk"]);
                }
                else if (animations.ContainsKey("idle"))
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, animations["idle"]);
                }
            }
        }

        overrideController.ApplyOverrides(overrides);
        AssetDatabase.SaveAssets();

        return overrideController;
    }

    private void CreateOrUpdatePrefab(string path, AnimatorOverrideController overrideController, Dictionary<string, AnimationClip> animations)
    {
        string folderName = new DirectoryInfo(path).Name.Replace(" ", "");
        // 프리팹을 상위 폴더에 저장하도록 경로 설정
        string parentFolderPath = Directory.GetParent(path).FullName.Replace("\\", "/").Replace(Application.dataPath, "Assets");
        string prefabPath = Path.Combine(parentFolderPath, $"{folderName}.prefab");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            // 프리팹 생성
            GameObject enemyPrefab = new GameObject(folderName);

            SpriteRenderer spriteRenderer = enemyPrefab.AddComponent<SpriteRenderer>();
            Animator animator = enemyPrefab.AddComponent<Animator>();

            // 첫 번째 스프라이트 설정
            if (animations.ContainsKey("idle"))
            {
                AnimationClip idleClip = animations["idle"];
                ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(idleClip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                if (keyframes != null && keyframes.Length > 0)
                {
                    spriteRenderer.sprite = keyframes[0].value as Sprite;
                }
            }

            animator.runtimeAnimatorController = overrideController;
            spriteRenderer.sortingLayerName = "Enemy";

            // TextPoint와 HitPoint 생성
            CreateChildObject(enemyPrefab.transform, "TextPoint", spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.max.y : 1);
            CreateChildObject(enemyPrefab.transform, "HitPoint", spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.center.y : 0);

            // Enemy 스크립트 추가
            Enemy enemyScript = enemyPrefab.AddComponent<Enemy>();
            enemyScript.TextPoint = enemyPrefab.transform.Find("TextPoint").gameObject;
            enemyScript.hitPoint = enemyPrefab.transform.Find("HitPoint").gameObject;

            PrefabUtility.SaveAsPrefabAsset(enemyPrefab, prefabPath);
            DestroyImmediate(enemyPrefab);
        }
        else
        {
            // 프리팹 업데이트
            GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Animator animator = prefabInstance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = prefabInstance.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = overrideController;

            SpriteRenderer spriteRenderer = prefabInstance.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = prefabInstance.AddComponent<SpriteRenderer>();
            }

            // 첫 번째 스프라이트 설정
            if (animations.ContainsKey("idle"))
            {
                AnimationClip idleClip = animations["idle"];
                ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(idleClip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                if (keyframes != null && keyframes.Length > 0)
                {
                    spriteRenderer.sprite = keyframes[0].value as Sprite;
                }
            }

            spriteRenderer.sortingLayerName = "Enemy";

            // TextPoint와 HitPoint 위치 업데이트
            UpdateChildObject(prefabInstance.transform, "TextPoint", spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.max.y : 1);
            UpdateChildObject(prefabInstance.transform, "HitPoint", spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.center.y : 0);

            PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
            DestroyImmediate(prefabInstance);
        }

        Debug.Log($"프리팹 '{folderName}'이(가) 생성 또는 업데이트되었습니다: {prefabPath}");
    }

    private void CreateChildObject(Transform parent, string name, float yPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent);
        child.transform.localPosition = new Vector3(0, yPosition, 0);
    }

    private void UpdateChildObject(Transform parent, string name, float yPosition)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            child.localPosition = new Vector3(0, yPosition, 0);
        }
        else
        {
            CreateChildObject(parent, name, yPosition);
        }
    }
}
