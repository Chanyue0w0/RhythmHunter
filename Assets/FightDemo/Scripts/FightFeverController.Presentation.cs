using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace RhythmHunter.FightDemo
{
    public sealed partial class FightFeverController
    {
        private Canvas ui;
        private RectTransform show, dialogue, barRoot;
        private Image mask, gem, fill, flash;
        private Text hint, body, heading, aIcon;
        private Image emptyExample, fullExample;
        private Font font;
        private Texture2D gradient;
        private Sprite gradientSprite;
        private GameObject stage;
        private Camera stageCamera;
        private RenderTexture stageTexture;
        private RawImage heroesImage;
        private readonly List<(SpriteRenderer source, SpriteRenderer copy)> copies = new();
        private readonly List<GameObject> effects = new();
        private GameObject gemEffect;
        private FightVfxOpacity gemOpacity;
        private readonly Vector3 stageOffset = new(16000, 16000, 16000);
        private bool particlesPaused;
        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var r = FightMenuUi.Rect(name, parent, position, size);
            r.anchorMin = r.anchorMax = r.pivot = Vector2.one * .5f; return r;
        }
        private static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private Text Text(Transform parent, string value, Vector2 pos, Vector2 size, int fontSize)
        {
            var label = Rect(parent, "Text", pos, size).gameObject.AddComponent<Text>();
            label.text = value; label.font = font; label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; return label;
        }
        private static Image Image(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var image = Rect(parent, name, pos, size).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private void BuildUi()
        {
            ui = new GameObject("FeverPresentation", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            ui.renderMode = RenderMode.ScreenSpaceOverlay; ui.sortingOrder = 200;
            var scaler = ui.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Noto Sans CJK TC", "Arial" }, 28);
            show = Rect(ui.transform, "FeverStage", Vector2.zero, Vector2.zero); Stretch(show);
            mask = Image(show, "BlackVeil", Vector2.zero, Vector2.zero, Color.black); Stretch(mask.rectTransform);
            heroesImage = Rect(show, "HeroesAndEnergy", Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            Stretch(heroesImage.rectTransform); heroesImage.raycastTarget = false;
            gem = Image(show, "ReleasedGem", Vector2.zero, new Vector2(82, 106), Color.white); gem.preserveAspect = true;
            barRoot = Rect(show, "FiveSegmentCharge", Vector2.zero, new Vector2(310, 30));
            Image(barRoot, "Border", Vector2.zero, new Vector2(320, 40), new Color(.45f, .72f, 1));
            Image(barRoot, "Empty", Vector2.zero, new Vector2(310, 30), new Color(.025f, .045f, .09f));
            gradient = new Texture2D(128, 1, TextureFormat.RGBA32, false) { name = "Fever white blue gradient", wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 128; x++)
            {
                float t = x / 127f;
                gradient.SetPixel(x, 0, t < .5f ? Color.Lerp(Color.white, new Color(.48f, .82f, 1), t * 2) : Color.Lerp(new Color(.48f, .82f, 1), new Color(.06f, .2f, .85f), (t - .5f) * 2));
            }
            gradient.Apply(); gradientSprite = Sprite.Create(gradient, new Rect(0, 0, 128, 1), Vector2.one * .5f);
            fill = Image(barRoot, "Charge", Vector2.zero, new Vector2(310, 30), Color.white);
            fill.sprite = gradientSprite; fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            for (int i = 1; i < 5; i++) Image(barRoot, "Divider" + i, new Vector2(-155 + 62 * i, 0), new Vector2(3, 30), new Color(.08f, .15f, .25f));
            aIcon = Text(barRoot, "A", new Vector2(0, 55), new Vector2(56, 56), 38);
            var disk = Image(barRoot, "AButtonBackground", new Vector2(0, 55), new Vector2(58, 58), new Color(.025f, .22f, .18f));
            disk.transform.SetSiblingIndex(aIcon.transform.GetSiblingIndex()); aIcon.transform.SetAsLastSibling();
            flash = Image(show, "ReleaseFlash", Vector2.zero, Vector2.zero, Color.clear); Stretch(flash.rectTransform);
            hint = Text(ui.transform, "", new Vector2(0, -270), new Vector2(1100, 90), 25);
            hint.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
            dialogue = Rect(ui.transform, "FeverLesson", Vector2.zero, new Vector2(920, 430));
            Image(dialogue, "Panel", Vector2.zero, new Vector2(920, 430), new Color(.025f, .065f, .12f, .98f));
            heading = Text(dialogue, "", new Vector2(0, 155), new Vector2(850, 65), 34);
            body = Text(dialogue, "", new Vector2(0, 45), new Vector2(820, 150), 27);
            emptyExample = Image(dialogue, "EmptyGem", new Vector2(-135, -80), new Vector2(65, 85), Color.white);
            fullExample = Image(dialogue, "FullGem", new Vector2(135, -80), new Vector2(65, 85), Color.white);
            emptyExample.preserveAspect = fullExample.preserveAspect = true;
            var button = FightMenuUi.Button(dialogue, "A　繼續", new Vector2(0, -170), new Vector2(260, 46), font, AdvancePage);
            var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            var textRect = button.GetComponentInChildren<Text>().rectTransform; textRect.anchorMin = textRect.anchorMax = textRect.pivot = Vector2.one * .5f;
            show.gameObject.SetActive(false); dialogue.gameObject.SetActive(false);
        }
        private void ShowPage()
        {
            dialogue.gameObject.SetActive(true); hint.text = "";
            bool examples = CurrentPhase == Phase.Introduction && page == 0;
            emptyExample.gameObject.SetActive(examples); fullExample.gameObject.SetActive(examples);
            emptyExample.sprite = presenter.FeverEmptyGem; fullExample.sprite = presenter.FeverFullGem;
            if (CurrentPhase == Phase.Explanation)
            {
                heading.text = "認識隊伍的血量與護甲";
                body.text = "愛心代表血量，由全隊勇者的血量加總。\n盾牌代表護甲，取決於首位勇者的天生護甲值。\n每次戰勝一波敵人後，護甲都會恢復至全滿。";
            }
            else if (page == 0)
            { heading.text = "累積魔力"; body.text = "每次呼叫勇者，都會累積魔力。\n魔力儲存在寶石中，充滿時就會亮起！\n左：未充能　　　　　右：充能已滿"; }
            else
            { heading.text = "解放Fever能量"; body.text = "寶石充滿後，按A啟動\"團隊共鳴\"。\n連續擊打解放寶石力量，施放強力絕招。"; }
        }
        private Vector2 ScreenToUi(Vector2 screen)
        { RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ui.transform, screen, null, out var p); return p; }
        private void BeginStage()
        {
            stage = new GameObject("Fever Isolated Render Stage"); stage.transform.position = stageOffset;
            var cameraObject = new GameObject("Fever Hero Camera"); cameraObject.transform.SetParent(stage.transform);
            stageCamera = cameraObject.AddComponent<Camera>(); stageCamera.CopyFrom(battleCamera);
            stageCamera.cullingMask = 1 << 31; stageCamera.clearFlags = CameraClearFlags.SolidColor; stageCamera.backgroundColor = Color.clear;
            stageCamera.allowHDR = false; stageCamera.allowMSAA = false; stageCamera.useOcclusionCulling = false;
            var data = stageCamera.GetUniversalAdditionalCameraData(); data.SetRenderer(2); data.renderPostProcessing = false; data.renderShadows = false; data.volumeLayerMask = 0;
            EnsureTexture();
            foreach (var hero in fight.FeverHeroes)
            {
                if (hero == null || hero.ActorInstance == null) continue;
                foreach (var source in hero.ActorInstance.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var copy = new GameObject(source.name + " Fever View").AddComponent<SpriteRenderer>();
                    copy.transform.SetParent(stage.transform); copy.gameObject.layer = 31;
                    copy.sharedMaterial = source.sharedMaterial; copies.Add((source, copy));
                }
            }
            gemEffect = CreateEffect(stage.transform);
            gemOpacity = gemEffect.GetComponent<FightVfxOpacity>() ?? gemEffect.AddComponent<FightVfxOpacity>();
            gemOpacity.SetOpacity(.04f);
        }
        private void EnsureTexture()
        {
            if (stageTexture && stageTexture.width == Screen.width && stageTexture.height == Screen.height) return;
            if (stageTexture) { stageCamera.targetTexture = null; stageTexture.Release(); Destroy(stageTexture); }
            stageTexture = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24, RenderTextureFormat.ARGB32);
            stageTexture.Create(); stageCamera.targetTexture = stageTexture; heroesImage.texture = stageTexture;
        }
        private GameObject CreateEffect(Transform parent)
        {
            var effect = Instantiate(chargeEffectPrefab, parent); effects.Add(effect);
            foreach (var t in effect.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            foreach (var light in effect.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var cfx in effect.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true)) cfx.enabled = false;
            foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main; main.loop = true; main.stopAction = ParticleSystemStopAction.None;
                ps.Play(false);
            }
            return effect;
        }
        private void ReleaseToHeroes()
        {
            foreach (var hero in fight.FeverHeroes)
            {
                if (!hero || !hero.ActorInstance) continue;
                var effect = CreateEffect(stage.transform);
                effect.name = "HeroEnergy_" + hero.name;
                foreach (var renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>(true)) renderer.sortingOrder = -10;
                // These render-stage anchors follow the live actor every frame.
                var foot = hero.ActorInstance.transform.Find("FootAnchor");
                heroEffects.Add((hero, foot ? foot : hero.ActorInstance.transform, effect.transform));
            }
        }
        private readonly List<(FightUnitSlot hero, Transform foot, Transform effect)> heroEffects = new();
        private void Present(float weight)
        {
            var heroes = fight.FeverHeroes;
            Vector3 center = Vector3.zero; int count = 0;
            foreach (var hero in heroes) if (hero && hero.ActorInstance) { center += hero.ActorInstance.transform.position; count++; }
            if (count == 0) { Abort(); return; }
            center = center / count + Vector3.up * 2.1f;
            cameraRig?.SetFeverFocus(center, weight, cameraZoom * weight);
            Vector2 shake = new Vector2(Mathf.Sin(phaseTime * 91), Mathf.Sin(phaseTime * 73)) * (.045f * pulse * weight);
            cameraRig?.SetFeverShake(shake);
            EnsureTexture();
            stageCamera.transform.SetPositionAndRotation(battleCamera.transform.position + stageOffset, battleCamera.transform.rotation);
            stageCamera.fieldOfView = battleCamera.fieldOfView; stageCamera.orthographic = battleCamera.orthographic; stageCamera.orthographicSize = battleCamera.orthographicSize;
            foreach (var pair in copies)
            {
                if (!pair.source || !pair.copy) continue;
                pair.copy.enabled = pair.source.enabled && pair.source.gameObject.activeInHierarchy;
                pair.copy.sprite = pair.source.sprite; pair.copy.color = pair.source.color;
                pair.copy.flipX = pair.source.flipX; pair.copy.flipY = pair.source.flipY;
                pair.copy.sortingOrder = pair.source.sortingOrder;
                pair.copy.transform.SetPositionAndRotation(pair.source.transform.position + stageOffset, pair.source.transform.rotation);
                pair.copy.transform.localScale = pair.source.transform.lossyScale;
            }
            var middle = heroes.Length > 1 && heroes[1] ? heroes[1] : heroes[0];
            Vector3 head = middle.HeadAnchor ? middle.HeadAnchor.position : middle.ActorInstance.transform.position + Vector3.up * 2;
            Vector3 gemWorld = head + battleCamera.transform.up * .8f;
            var target = ScreenToUi(battleCamera.WorldToScreenPoint(gemWorld));
            var position = Vector2.Lerp(gemOrigin, target, CurrentPhase == Phase.Flight ? weight : 1);
            position += new Vector2(Mathf.Sin(phaseTime * 55), Mathf.Sin(phaseTime * 67)) * (Progress * 3 + pulse * 5);
            gem.rectTransform.anchoredPosition = position;
            gem.rectTransform.localScale = Vector3.one * (1 + Progress * .18f + pulse * .08f);
            gem.color = new Color(1, 1, 1, weight);
            barRoot.anchoredPosition = target + new Vector2(Mathf.Sin(phaseTime * 80) * pulse * 6, 92 + Mathf.Cos(phaseTime * 63) * pulse * 3);
            aIcon.rectTransform.localScale = Vector3.one * (1 + pulse * .18f);
            fill.fillAmount = Mathf.Clamp01(displayProgress + overshoot);
            float veilWeight = CurrentPhase == Phase.Flight ? Mathf.SmoothStep(0, 1, phaseTime / .25f) : weight;
            mask.color = new Color(0, 0, 0, veilWeight);
            heroesImage.color = new Color(1, 1, 1, veilWeight);
            if (CurrentPhase == Phase.Charging) hint.text = "連打 A　解放能量！";
            if (CurrentPhase == Phase.Returning) hint.text = lastSuccess ? "" : "能量解放失敗";
            flash.color = new Color(.75f, .9f, 1, CurrentPhase == Phase.Released ? Mathf.Max(0, .85f - phaseTime * 3) : 0);
            if (gemEffect)
            {
                // Anchor at the displayed gem's bottom, including its animated scale and shake.
                var bottom = gem.rectTransform.TransformPoint(new Vector3(0, gem.rectTransform.rect.yMin, 0));
                var screen = RectTransformUtility.WorldToScreenPoint(null, bottom);
                float depth = Vector3.Dot(gemWorld - battleCamera.transform.position, battleCamera.transform.forward);
                gemEffect.transform.position = battleCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth)) + stageOffset;
                gemEffect.transform.rotation = battleCamera.transform.rotation;
                gemEffect.transform.localScale = Vector3.one * Mathf.Lerp(.22f, .55f, Progress);
                float opacity = Mathf.Lerp(.04f, 1, Progress);
                gemOpacity.SetOpacity(opacity);
            }
            foreach (var item in heroEffects)
                if (item.hero && item.foot && item.effect)
                {
                    item.effect.position = item.foot.position + stageOffset;
                    item.effect.rotation = battleCamera.transform.rotation;
                    item.effect.localScale = Vector3.one * .75f;
                }
        }
        private void SetStagePaused(bool paused)
        {
            if (particlesPaused == paused) return; particlesPaused = paused;
            foreach (var effect in effects) if (effect)
                foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
                    if (paused) ps.Pause(false); else ps.Play(false);
        }
        private void CleanupStage()
        {
            cameraRig?.SetFeverFocus(Vector3.zero, 0, 0); cameraRig?.SetFeverShake(Vector2.zero);
            if (stageCamera) stageCamera.targetTexture = null;
            if (stage) { stage.SetActive(false); Destroy(stage); }
            if (stageTexture) { stageTexture.Release(); Destroy(stageTexture); }
            copies.Clear(); effects.Clear(); heroEffects.Clear(); particlesPaused = false;
            if (heroesImage) heroesImage.texture = null;
        }
    }
}
