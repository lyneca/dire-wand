using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Wand;

public class TutorialSave : CustomData {
    public static TutorialSave _local;

    public static TutorialSave local => _local ?? Load();

    public int currentTier = 0;
    public Dictionary<string, bool>[] spellsCast;
    public int numTiers;

    public TutorialSave() {
        var spells = Catalog.GetData<ItemData>("Wand").GetModule<ItemModuleWand>().spells
            .Where(spell => spell.showInTutorial).ToList();
        foreach (var spell in spells) {
            if (spell.tier > numTiers) numTiers = spell.tier;
        }

        numTiers++;

        spellsCast = new Dictionary<string, bool>[numTiers];
        for (var i = 0; i < spellsCast.Length; i++) {
            spellsCast[i] = new Dictionary<string, bool>();
        }

        foreach (var spell in spells) {
            spellsCast[spell.tier][spell.title] = false;
        }
    }

    public static void Save() {
        DataManager.SaveLocalFile(local, "wand.sav");
    }

    public static TutorialSave Load() {
        try {
            _local = DataManager.LoadLocalFile<TutorialSave>("wand.sav");
        } catch {
            _local = null;
        }
        if (_local != null) return _local;
        
        _local = new TutorialSave();
        Refresh();
        Save();

        return _local;
    }

    public static void Cast(int tier, string title) {
        if (local.spellsCast[tier].TryGetValue(title, out bool cast) && cast) return;
        local.spellsCast[tier][title] = true;
        Refresh();
        Save();
    }

    public static bool HasCast(string title) {
        foreach (var dict in local.spellsCast) {
            if (dict.TryGetValue(title, out bool cast)) return cast;
        }

        return false;
    }

    public static void Refresh() {
        if (local.currentTier >= local.numTiers - 1) return;
        foreach (var kvp in local.spellsCast[local.currentTier]) {
            if (!kvp.Value) return;
        }

        local.currentTier++;
        DisplayMessage.instance.ShowMessage(new DisplayMessage.MessageData($"Tier {local.currentTier + 1} Dire Wand spell tutorials unlocked.", "", "", "", 1));
    }
}

public class Tutorial : WandModule {
    private static readonly int BaseColor = Shader.PropertyToID("BaseColor");
    public List<TutorialData> tutorials;
    public string iconMatAddress = "Lyneca.Wand.Tutorial.IconMat";
    public string tutorialPrefabAddress = "Lyneca.Wand.Tutorial.Prefab";
    public int cols = 6;
    public float distance = 2.4f;
        
    public Material iconMat;
    public GameObject tutorialPrefab;
    private TutorialWindow window;
    private List<TutorialOrb> orbs;
    private int rows;
    public StateTracker state;

    public GameObject wandPrefab;
    public GameObject handLeftPrefab;
    public GameObject handRightPrefab;

    public GameObject holoWand;
    public GameObject handLeft;
    public GameObject handRight;

    public override WandModule Clone() {
        if (!(base.Clone() is Tutorial clone)) return null;
        if (tutorials != null)
            clone.tutorials = new List<TutorialData>(tutorials);
        return clone;
    }

    public override void OnInit() {
        base.OnInit();
        
        Catalog.LoadAssetAsync<GameObject>("Lyneca.Wand.Tutorial.HandLeft", obj => handLeftPrefab = obj, "Wand Tutorial");
        Catalog.LoadAssetAsync<GameObject>("Lyneca.Wand.Tutorial.HandRight", obj => handRightPrefab = obj, "Wand Tutorial");
        Catalog.LoadAssetAsync<GameObject>("Lyneca.Wand.Tutorial.Wand", obj => wandPrefab = obj, "Wand Tutorial");

        orbs = new List<TutorialOrb>();
        tutorials ??= new List<TutorialData>();
        for (var i = 0; i < TutorialSave.local.numTiers; i++) {
            foreach (var spell in wand.spells) {
                if (spell.tier != i || !spell.showInTutorial) continue;
                string spellTitle = spell.title ?? spell.GetType().Name;
                tutorials.Add(new TutorialData {
                    id = "Tutorial: " + spellTitle,
                    title = spellTitle,
                    description = spell.description,
                    color = spell.color,
                    iconAddress = spell.iconAddress,
                    tier = spell.tier,
                    gesture = spell.gesture,
                    videoAddresses = new List<string>(spell.videoAddresses ?? new List<string>())
                });
            }
        }

        rows = tutorials.Count / cols;
            
        Catalog.LoadAssetAsync<GameObject>(tutorialPrefabAddress, prefab => tutorialPrefab = prefab, "TutorialModule");
        Catalog.LoadAssetAsync<Material>(iconMatAddress, mat => iconMat = mat, "TutorialModule");
            
        for (var index = 0; index < tutorials.Count; index++) {
            var tutorial = tutorials[index];
            tutorial.Load(this);
        }

        wand.button
            .Then(() => wand.holdingHand.Buttoning() && wand.holdingHand.Triggering(), "Hold Button and Trigger", 1f)
            .Do(() => wand.StartCoroutine(Open()), "Open Tutorial");
        wand.trigger
            .Then(() => wand.holdingHand.Buttoning() && wand.holdingHand.Triggering(), "Hold Button and Trigger", 1f)
            .Do(() => wand.StartCoroutine(Open()), "Open Tutorial");

        state = new StateTracker()
            .On(() => wand.item.mainHandler?.Triggering() == true, TrySelect);
    }

    public IEnumerator Open() {
        MarkCasted();
        var i = 0;
        if (handLeft) Object.Destroy(handLeft);
        if (handRight) Object.Destroy(handRight);
        if (holoWand) Object.Destroy(holoWand);

        while (wandPrefab == null || handLeftPrefab == null || handRightPrefab == null) yield return 0;
        holoWand = Object.Instantiate(wandPrefab, wand.transform.position, wand.transform.rotation);
        handLeft = Object.Instantiate(handLeftPrefab, wand.transform.position, wand.transform.rotation);
        handLeft.SetActive(false);
        handRight = Object.Instantiate(handRightPrefab, wand.transform.position, wand.transform.rotation);
        holoWand.transform.SetParent(handRight.transform.Find("Offset/gripTrans"));
        holoWand.transform.localPosition = Vector3.zero;
        holoWand.transform.localRotation = Quaternion.identity;
        handRight.SetActive(false);

        var rig = new PlayerRig(handLeft, handRight, Player.currentCreature.ragdoll.rootPart.transform,
            Player.local.head.transform);
        
        foreach (var data in tutorials) {
            var orb = data.SpawnOrb();
            orbs.Add(orb);

            int row = rows - i / cols;
            int col = i % cols;

            var position
                = Player.local.head.transform.position
                  - Vector3.up * 0.5f
                  + Quaternion.AngleAxis(((float)col).RemapClamp(0, cols, -40, 40), Vector3.up)
                  * lookDir.normalized
                  * distance
                  + Vector3.up * (row * 0.5f);
            orb.transform.position = position;
        }

        lookDir = Player.local.head.transform.forward;

        var debounce = 0;
        float animation = 0;
        var active = true;
        TutorialOrb selectedOrb = null;
        GestureStep lastGesture = null;
            
        while (active) {
            switch (debounce) {
                case 0 when !wand.holdingHand.Buttoning():
                    debounce = 1;
                    break;
                case 1 when wand.holdingHand.Buttoning():
                    debounce = 2;
                    break;
                case 2 when !wand.holdingHand.Buttoning():
                    active = false;
                    break;
            }

            if (window) {
                var position = Player.local.head.transform.position - Vector3.up * 0.5f
                               + Vector3.ProjectOnPlane(Player.local.head.transform.forward, Vector3.up).normalized
                               * distance
                               + Vector3.up * 0.5f;
                window.transform.SetPositionAndRotation(
                    Vector3.Lerp(window.transform.position, position, Time.deltaTime * 10f),
                    Quaternion.Slerp(window.transform.rotation,
                        Quaternion.LookRotation(Vector3.ProjectOnPlane(window.transform.position - Player.local.head.transform.position, Vector3.up)),
                        Time.deltaTime * 10f));
                if (window.data?.gesture != null) {
                    animation += Time.deltaTime * 5;
                    if (animation > window.data.gesture.Length + 5f) animation = -5;
                    int gestureIndex = Mathf.FloorToInt(animation);
                    if (gestureIndex <= 0)
                        gestureIndex = 0;
                    if (gestureIndex >= window.data.gesture.Length)
                        gestureIndex = window.data.gesture.Length - 1;
                    if (window.data.gestures[gestureIndex] == null) {
                        lastGesture?.UpdateTargets(rig, 1, forceGripping: Gesture.HandSide.Right);
                    } else {
                        lastGesture = window.data.gestures[gestureIndex];
                        window.data.gestures[gestureIndex]
                            .UpdateTargets(rig, (animation - gestureIndex).RemapClamp(0, 0.5f, 0, 1), forceGripping: Gesture.HandSide.Right);
                    }
                } else {
                    handLeft.SetActive(false);
                    handRight.SetActive(false);
                }
            } else {
                var oldOrb = selectedOrb;
                if (Physics.Raycast(wand.tipRay, out var hit, 5,
                        LayerMask.GetMask(LayerName.DroppedItem.ToString()),
                        QueryTriggerInteraction.Collide)
                    && hit.collider.GetComponentInParent<TutorialOrb>() is { locked: false } newOrb) {
                    selectedOrb = newOrb;
                } else {
                    selectedOrb = null;
                }

                if (selectedOrb != oldOrb) {
                    if (oldOrb)
                        oldOrb.highlighted = false;
                    if (selectedOrb) {
                        wand.item.Haptic(0.5f);
                        selectedOrb.highlighted = true;
                    }
                }
                state.Update();
                for (var index = 0; index < orbs.Count; index++) {
                    var orb = orbs[index];
                    UpdateOrb(orb, index);
                }
            }

            yield return 0;
        }
        Close();
    }

    public void TrySelect() {
        if (!(Physics.Raycast(wand.tipRay, out var hit, 5,
                  LayerMask.GetMask(LayerName.DroppedItem.ToString()),
                  QueryTriggerInteraction.Collide)
              && hit.collider.GetComponentInParent<TutorialOrb>() is
                  { data: TutorialData data, locked: false })) return;

        OnSelect(data);
    }

    private Vector3 lookDir;

    public void UpdateOrb(TutorialOrb orb, int index) {
        int row = rows - index / cols;
        int col = index % cols;

        if (Vector3.Angle(lookDir, Player.local.head.transform.forward) > 30) {
            lookDir = Vector3.Slerp(lookDir, Player.local.head.transform.forward, Time.deltaTime * 10);
        }
        var headLook = Vector3.ProjectOnPlane(Player.local.head.transform.forward, Vector3.up);
            
        if (Vector3.Angle(lookDir, headLook) > 60) {
            lookDir = headLook;
        } else {
            lookDir = Vector3.ProjectOnPlane(lookDir, Vector3.up);
        }

        var position
            = Player.local.head.transform.position - Vector3.up * 0.5f
              + Quaternion.AngleAxis(((float)col).RemapClamp(0, cols, -40, 40), Vector3.up)
              * lookDir.normalized
              * distance
              + Vector3.up * (row * 0.5f);

        orb.material?.SetColor(BaseColor,
            orb.data.color * (orb.locked ? 0f : (orb.highlighted ? 8 : 1) * (orb.uncast ? 0.2f : 1)));
            
        orb.transform.position = Vector3.Lerp(orb.transform.position, position, Time.deltaTime * 10);
        orb.transform.rotation = Quaternion.Slerp(orb.transform.rotation,
            Quaternion.LookRotation(orb.transform.position - Player.local.head.transform.position),
            Time.deltaTime * 10);
    }

    public void OnSelect(TutorialData data) {
        Close();
        window = Object.Instantiate(tutorialPrefab).AddComponent<TutorialWindow>();
        window.transform.position = Player.local.head.transform.position
                                    - Vector3.up * 0.5f
                                    + Vector3.ProjectOnPlane(Player.local.head.transform.forward, Vector3.up)
                                        .normalized
                                    * distance
                                    + Vector3.up * 0.5f;
        window.Load(data);
    }

    public void Close() {
        wand.item.Haptic(1);
        for (var index = 0; index < orbs.Count; index++) {
            var orb = orbs[index];
            Object.Destroy(orb.gameObject);
        }
        orbs.Clear();

        if (window) {
            Object.Destroy(handLeft);
            Object.Destroy(handRight);
            Object.Destroy(handRight);

            handLeft = null;
            handRight = null;
            holoWand = null;
            Object.Destroy(window.gameObject);
        }

        window = null;
    }
}
    
public class TutorialWindow : MonoBehaviour {
    private Text title;
    private Text description;
    private VideoPlayer videoPlayer;
    private GameObject videoRect;
    private int index;
    public TutorialData data;

    public void Awake() {
        title = transform.Find("Elements/Title").GetComponent<Text>();
        description = transform.Find("Elements/Description").GetComponent<Text>();
        videoRect = transform.Find("Elements/VideoContainer").gameObject;
        videoPlayer = transform.Find("Video").GetComponent<VideoPlayer>();
        videoPlayer.isLooping = false;
        videoPlayer.loopPointReached += _ => Next();
    }

    public void Load(TutorialData instance) {
        videoPlayer.clip = null;
        data = instance;
        title.text = instance.title ?? "";
        description.text = instance.description ?? "";
        videoPlayer.clip = instance.videos.Count > 0 ? instance.videos[0] : null;
        videoRect.SetActive(instance.videos.Count > 0);
        videoPlayer.Stop();
        videoPlayer.Play();
    }

    public void Next() {
        index++;
        index %= data.videos.Count;
        videoPlayer.clip = data.videos[index];
        videoPlayer.Play();
    }
}

public class TutorialOrb : MonoBehaviour {
    public TutorialData data;
    public bool highlighted = false;
    public bool uncast = false;
    public bool locked = false;
    public Material material;
}
    
public class TutorialData {
    public string id;
    public string title;
    public string description;
    public string[] gesture;
    public Color color;
    public List<string> videoAddresses;
    public string iconAddress;
    private Tutorial module;
    public List<GestureStep> gestures;
        
    [NonSerialized]
    public List<VideoClip> videos;
    [NonSerialized]
    public Texture icon;

    private static readonly int BaseTexture = Shader.PropertyToID("BaseTexture");
    private static readonly int BaseColor = Shader.PropertyToID("BaseColor");
    public int tier;

    public void Load(Tutorial module) {
        this.module = module;
        gestures = new List<GestureStep>();
        if (gesture != null) {
            var log = new List<string> { $"Spell {id}:" };
            foreach (string step in gesture) {
                var gestureStep = GestureStep.FromString(step);
                gestures.Add(gestureStep);
                if (gestureStep != null)
                    log.Add($"- {gestureStep.Description}");
            }
            // Debug.Log("\n".Join(log));
        }
        videos = new List<VideoClip>();
        if (videoAddresses is { Count: > 0 }) {
            for (var index = 0; index < videoAddresses.Count; index++) {
                string videoAddress = videoAddresses[index];
                Catalog.LoadAssetAsync<VideoClip>(videoAddress, clip => videos.Add(clip), id);
            }
        }

        if (string.IsNullOrEmpty(iconAddress)) return;
        Catalog.LoadAssetAsync<Texture>(iconAddress, image => icon = image, id);
    }

    public TutorialOrb SpawnOrb() {
        var parent = new GameObject();
            
        var orb = GameObject.CreatePrimitive(PrimitiveType.Plane);
        orb.transform.SetParent(parent.transform);
        orb.transform.localPosition = Vector3.zero;
        orb.transform.localRotation = Quaternion.AngleAxis(180, Vector3.up) * Quaternion.FromToRotation(Vector3.up, Vector3.forward);
        orb.transform.localScale = Vector3.one;
        Object.Destroy(orb.GetComponent<Collider>());
        var box = orb.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = new Vector3(10, 1f, 10);
        box.isTrigger = true;
        orb.GetComponent<MeshRenderer>().material = module.iconMat;
        orb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        if (icon)
            orb.GetComponent<MeshRenderer>().material.SetTexture(BaseTexture, icon);
        orb.GetComponent<MeshRenderer>().material.SetColor(BaseColor, color);
        orb.gameObject.layer = GameManager.GetLayer(LayerName.DroppedItem);
            
        parent.transform.localScale = Vector3.one * 0.03f;
            
        var link = parent.AddComponent<TutorialOrb>();
        link.uncast = !TutorialSave.HasCast(title);
        if (TutorialSave.local.currentTier < tier)
            link.locked = true;
        link.material = orb.GetComponent<MeshRenderer>().material;
        link.data = this;
        return link;
    }
}