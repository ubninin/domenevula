using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메뉴(ESC) + 모드 토글(C키)에 필요한 UI 오브젝트와 스크립트 연결을 한 번에 만들어주는 메뉴.
///
/// 메뉴 위치: 상단 메뉴 도메네뷸라 > 메뉴/모드 UI 자동 구성
/// 하는 일:
///   1) GameMenuRoot 아래에 CommandPanel(배경, 커맨드 텍스트 5개, 선택 하이라이트, 모드 표시 텍스트) 생성
///   2) GameMenuController 인스펙터 필드 전부 연결 (없으면 GameMenuRoot에 붙임)
///   3) PartyStatusRoot에 PartyStatusCursor, PartySlot1~3에 PartySlotUI 붙임
///   4) Systems 아래에 ShootModeController(MapShootModeController) 생성
///   5) Canvas_Screen 아래에 ModeToastText(ModeToast) 생성 후 연결
/// 이미 있는 오브젝트/컴포넌트는 새로 만들지 않고 재사용하므로 여러 번 눌러도 중복되지 않는다.
/// 끝나면 씬이 수정됨 상태가 되니 Ctrl+S로 저장할 것. 생성된 위치/크기/색은 이후 자유롭게 손봐도 됨.
/// </summary>
public static class SetupGameMenuUI
{
    private static readonly string[] CommandNames = { "상태", "아이템", "장전", "저장", "종료" };
    private static readonly string[] CommandObjectNames = { "CommandStatus", "CommandItem", "CommandReload", "CommandSave", "CommandQuit" };

    [MenuItem("도메네뷸라/메뉴·모드 UI 자동 구성")]
    private static void Run()
    {
        GameMenuController controller = Object.FindFirstObjectByType<GameMenuController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            GameObject root = FindInScene("GameMenuRoot");
            if (root == null)
            {
                EditorUtility.DisplayDialog("메뉴 UI 구성", "씬에서 GameMenuRoot를 찾지 못했습니다.\nSampleScene이 열려 있는지 확인해주세요.", "확인");
                return;
            }
            controller = Undo.AddComponent<GameMenuController>(root);
        }

        Transform menuRoot = controller.transform;
        Transform canvas = menuRoot.GetComponentInParent<Canvas>() != null ? menuRoot.GetComponentInParent<Canvas>().transform : menuRoot.parent;
        TMP_FontAsset font = FindFont();

        // ── 1) 커맨드 패널 ──
        RectTransform panel = GetOrCreateUI(menuRoot, "CommandPanel", out bool panelNew);
        if (panelNew)
        {
            SetRect(panel, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -40), new Vector2(320, 480));
            Image bg = Undo.AddComponent<Image>(panel.gameObject);
            bg.color = new Color(0.05f, 0.08f, 0.15f, 0.88f);
            bg.raycastTarget = false;
        }

        RectTransform highlight = GetOrCreateUI(panel, "CursorHighlight", out bool hlNew);
        if (hlNew)
        {
            SetRect(highlight, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(290, 60));
            Image img = Undo.AddComponent<Image>(highlight.gameObject);
            img.color = new Color(0.47f, 0.84f, 1f, 0.35f);
            img.raycastTarget = false;
        }

        TMP_Text[] commandTexts = new TMP_Text[5];
        for (int i = 0; i < 5; i++)
        {
            RectTransform rt = GetOrCreateUI(panel, CommandObjectNames[i], out bool isNew);
            if (isNew)
            {
                SetRect(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30 - i * 70), new Vector2(280, 60));
                TextMeshProUGUI t = Undo.AddComponent<TextMeshProUGUI>(rt.gameObject);
                StyleText(t, font, CommandNames[i], 34, TextAlignmentOptions.Center);
            }
            commandTexts[i] = rt.GetComponent<TMP_Text>();
        }

        RectTransform modeLabelRt = GetOrCreateUI(panel, "ModeLabel", out bool modeNew);
        if (modeNew)
        {
            SetRect(modeLabelRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(280, 40));
            TextMeshProUGUI t = Undo.AddComponent<TextMeshProUGUI>(modeLabelRt.gameObject);
            StyleText(t, font, "현재 모드: 상호작용", 24, TextAlignmentOptions.Center);
        }

        // ── 2) GameMenuController 필드 연결 ──
        Transform statusRoot = menuRoot.Find("PartyStatusRoot");
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("commandPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("cursorHighlight").objectReferenceValue = highlight;
        SerializedProperty arr = so.FindProperty("commandTexts");
        arr.arraySize = 5;
        for (int i = 0; i < 5; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = commandTexts[i];
        if (statusRoot != null) so.FindProperty("partyStatusRoot").objectReferenceValue = statusRoot.gameObject;
        so.FindProperty("modeLabel").objectReferenceValue = modeLabelRt.GetComponent<TMP_Text>();
        so.ApplyModifiedProperties();

        // ── 3) 상태창 스크립트 ──
        int slotCount = 0;
        if (statusRoot != null)
        {
            if (statusRoot.GetComponent<PartyStatusCursor>() == null) Undo.AddComponent<PartyStatusCursor>(statusRoot.gameObject);
            foreach (Transform child in statusRoot)
            {
                if (!child.name.StartsWith("PartySlot")) continue;
                if (child.GetComponent<PartySlotUI>() == null) Undo.AddComponent<PartySlotUI>(child.gameObject);
                slotCount++;
            }
        }

        // ── 4) 모드 토글 컨트롤러 ──
        MapShootModeController shoot = Object.FindFirstObjectByType<MapShootModeController>(FindObjectsInactive.Include);
        if (shoot == null)
        {
            GameObject go = new GameObject("ShootModeController");
            Undo.RegisterCreatedObjectUndo(go, "ShootModeController 생성");
            GameObject systems = FindInScene("Systems");
            if (systems != null) go.transform.SetParent(systems.transform, false);
            shoot = Undo.AddComponent<MapShootModeController>(go);
        }

        // ── 5) 토스트 ──
        ModeToast toast = Object.FindFirstObjectByType<ModeToast>(FindObjectsInactive.Include);
        if (toast == null)
        {
            RectTransform rt = GetOrCreateUI(canvas, "ModeToastText", out _);
            SetRect(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(700, 70));
            TextMeshProUGUI t = Undo.AddComponent<TextMeshProUGUI>(rt.gameObject);
            StyleText(t, font, "", 36, TextAlignmentOptions.Center);
            toast = Undo.AddComponent<ModeToast>(rt.gameObject);
        }
        SerializedObject shootSo = new SerializedObject(shoot);
        shootSo.FindProperty("toast").objectReferenceValue = toast;
        shootSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log($"[메뉴 UI 구성] 완료 - 파티 슬롯 {slotCount}개 연결. Ctrl+S로 씬을 저장하세요.");
    }

    // ── 헬퍼 ──

    private static GameObject FindInScene(string objectName)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == objectName && go.scene.IsValid() && !EditorUtility.IsPersistent(go))
                return go;
        }
        return null;
    }

    private static TMP_FontAsset FindFont()
    {
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font != null && t.GetComponentInParent<GameMenuController>() == null) return t.font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    private static RectTransform GetOrCreateUI(Transform parent, string objectName, out bool created)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            created = false;
            return existing as RectTransform;
        }
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, objectName + " 생성");
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        created = true;
        return go.GetComponent<RectTransform>();
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void StyleText(TextMeshProUGUI t, TMP_FontAsset font, string text, float size, TextAlignmentOptions align)
    {
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
    }
}
