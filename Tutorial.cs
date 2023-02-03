using System;
using System.Collections;
using System.Collections.Generic;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Wand {
    public class Tutorial : WandModule {
        private static readonly int BaseColor = Shader.PropertyToID("BaseColor");
        public List<TutorialData> tutorials;
        public string iconMatAddress = "Lyneca.Wand.Tutorial.IconMat";
        public string tutorialPrefabAddress = "Lyneca.Wand.Tutorial.Prefab";
        public int cols = 6;
        public float distance = 1.6f;
        
        public Material iconMat;
        public GameObject tutorialPrefab;
        private TutorialWindow window;
        private List<TutorialOrb> orbs;
        private int rows;
        public StateTracker state;

        public override WandModule Clone() {
            if (!(base.Clone() is Tutorial clone)) return null;
            if (tutorials != null)
                clone.tutorials = new List<TutorialData>(tutorials);
            return clone;
        }

        public override void OnInit() {
            base.OnInit();

            orbs = new List<TutorialOrb>();
            tutorials ??= new List<TutorialData>();
            foreach (var spell in wand.spells) {
                var spellTitle = spell.title ?? spell.GetType().Name;
                tutorials.Add(new TutorialData {
                    id = "Tutorial: " + spellTitle,
                    title = spellTitle,
                    description = spell.description,
                    color = spell.color,
                    iconAddress = spell.iconAddress,
                    videoAddresses = new List<string>(spell.videoAddresses ?? new List<string>())
                });
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
            foreach (var data in tutorials) {
                orbs.Add(data.SpawnOrb());
            }

            var debounce = 0;
            var active = true;
            TutorialOrb selectedOrb = null;
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
                            Quaternion.LookRotation(Vector3.ProjectOnPlane(window.transform.position - wand.otherHand.transform.position, Vector3.up)),
                            Time.deltaTime * 10f));
                } else {
                    var oldOrb = selectedOrb;
                    if (Physics.Raycast(wand.tipRay, out var hit, 5,
                            LayerMask.GetMask(LayerName.DroppedItem.ToString()),
                            QueryTriggerInteraction.Collide)
                        && hit.collider.GetComponentInParent<TutorialOrb>() is TutorialOrb newOrb) {
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
                  && hit.collider.GetComponentInParent<TutorialOrb>()?.data is TutorialData data)) return;
            
            OnSelect(data);
        }

        public void UpdateOrb(TutorialOrb orb, int index) {
            int row = rows - index / cols;
            int col = index % cols;

            var lookDir = wand.otherHand.PointDir;
            var headLook = Vector3.ProjectOnPlane(Player.local.head.transform.forward, Vector3.up);
            
            if (Vector3.Angle(lookDir, headLook) > 60) {
                lookDir = headLook;
            } else {
                lookDir = Vector3.ProjectOnPlane(lookDir, Vector3.up);
            }

            var position
                = wand.otherHand.transform.position
                  + Quaternion.AngleAxis(((float)col).RemapClamp(0, cols, -40, 40), Vector3.up)
                  * lookDir.normalized
                  * distance
                  + Vector3.up * (row * 0.5f);
            
            orb.material?.SetColor(BaseColor, orb.data.color * (orb.highlighted ? 5 : 1));
            
            orb.transform.position = Vector3.Lerp(orb.transform.position, position, Time.deltaTime * 10);
            orb.transform.rotation = Quaternion.Slerp(orb.transform.rotation,
                Quaternion.LookRotation(orb.transform.position - wand.otherHand.transform.position),
                Time.deltaTime * 10);
        }

        public void OnSelect(TutorialData data) {
            Close();
            window = Object.Instantiate(tutorialPrefab).AddComponent<TutorialWindow>();
            window.Load(data);
        }

        public void Close() {
            wand.item.Haptic(1);
            for (var index = 0; index < orbs.Count; index++) {
                var orb = orbs[index];
                Object.Destroy(orb.gameObject);
            }
            orbs.Clear();

            if (window)
                Object.Destroy(window.gameObject);
            window = null;
        }
    }
    
    public class TutorialWindow : MonoBehaviour {
        private Text title;
        private Text description;
        private VideoPlayer videoPlayer;
        private GameObject videoRect;
        private int index;
        private TutorialData data;

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
        public Material material;
    }
    
    public class TutorialData {
        public string id;
        public string title;
        public string description;
        public Color color;
        public List<string> videoAddresses;
        public string iconAddress;
        private Tutorial module;
        
        [NonSerialized]
        public List<VideoClip> videos;
        [NonSerialized]
        public Texture icon;

        private static readonly int BaseTexture = Shader.PropertyToID("BaseTexture");
        private static readonly int BaseColor = Shader.PropertyToID("BaseColor");

        public void Load(Tutorial module) {
            this.module = module;
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
            link.material = orb.GetComponent<MeshRenderer>().material;
            link.data = this;
            return link;
        }
    }
}