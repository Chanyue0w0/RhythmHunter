using RhythmHunter.RhythmDemo;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace RhythmHunter.FightDemo
{
    // Stage-owned data, normal input judgement and primary-contact events drive the lesson.
    [DisallowMultipleComponent]
    public sealed partial class FightTutorialController : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] private Font textFont;
        [SerializeField] private Vector2 dialogueSize = new(880, 360);
        [SerializeField, Range(0, 1)] private float backdropOpacity = .72f;
        [SerializeField, Min(18)] private int bodyFontSize = 30;
        [Header("Practice objective")]
        [SerializeField] private Vector2 objectivePosition = new(0, 210);
        private FightCombatController fight;
        private FightPauseController pause;
        private FightTutorialLesson lesson;
        private GameObject ui, modal, objective;
        private Text title, body, counter;
        private Font runtimeFont;
        private int index = -1, progress, enteredFrame;
        private bool advancePending, started;
        public bool Completed { get; private set; }
        public int StepIndex => index;
        public int Progress => progress;
        public bool Active => isActiveAndEnabled && lesson != null && !Completed;
        public bool Reading => Active && index >= 0 && Step.goal == FightTutorialLesson.Goal.Dialogue;
        public bool RhythmOnly => Active && index >= 0 && Step.goal == FightTutorialLesson.Goal.OnBeatCalls;
        public bool SuppressMageRetaliation => Active && (index < 0 || Step.suppressMageRetaliation || Reading);
        private FightTutorialLesson.Step Step => lesson.steps[index];

        private void Start()
        {
            fight = GetComponent<FightCombatController>();
            pause = GetComponent<FightPauseController>();
            var stage = GetComponent<FightLevelPlanner>()?.ActiveStage;
            lesson = stage != null ? stage.TutorialLesson : null;
            if (lesson == null || lesson.steps.Count == 0) { lesson = null; return; }
            fight.HeroCalled += OnHeroCalled;
            fight.SwordPrimaryHit += OnSwordHit;
            fight.EnemyAttackResolved += OnEnemyHit;
            fight.MageSlimeAttached += OnSlimeHit;
            fight.AimedFollowUpHit += OnFollowUpHit;
            fight.AimedShotHit += OnAimedShotHit;
            fight.ConfigureTutorialEnemies(lesson.controlEnemyAttacks, false, lesson.enemyAttackIntervalBeats, lesson.preventPlayerDeath);
            fight.BattleLost += Hide;
            fight.BattleVictory += Hide;
            fight.SetTutorialChargeSuppressed(true);
        }
        public void ConfigureUi(Transform parent, Font fallback)
        {
            if (ui != null) return;
            var root = FightMenuUi.Rect("GuidedTutorial", parent, Vector2.zero, Vector2.zero);
            Stretch(root); ui = root.gameObject;
            var font = textFont;
            if (font == null)
            {
                // Project has no CJK font asset yet. Use OS fallback; an authored font can replace it.
                runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "PingFang TC", "Noto Sans CJK TC", "Arial Unicode MS" }, bodyFontSize);
                font = runtimeFont != null ? runtimeFont : fallback;
            }
            ConfigurePositionBadges(root, font);
            var veil = FightMenuUi.Rect("ReadingMask", root, Vector2.zero, Vector2.zero);
            Stretch(veil); modal = veil.gameObject;
            veil.gameObject.AddComponent<Image>().color = new Color(.015f, .025f, .045f, backdropOpacity);
            var panel = FightMenuUi.Rect("Dialogue", veil, Vector2.zero, dialogueSize); Center(panel);
            panel.gameObject.AddComponent<Image>().color = new Color(.045f, .09f, .14f, .98f);
            title = Label(panel, font, new Vector2(0, 125), new Vector2(dialogueSize.x - 70, 55), 34);
            title.color = new Color(.55f, .9f, 1);
            body = Label(panel, font, new Vector2(0, 5), new Vector2(dialogueSize.x - 90, 175), bodyFontSize);
            body.alignment = TextAnchor.MiddleLeft;
            var next = FightMenuUi.Button(panel, "A　繼續", new Vector2(0, -125), new Vector2(340, 48), font, AdvanceDialogue);
            Center((RectTransform)next.transform);
            var nextText = next.GetComponentInChildren<Text>(); nextText.fontSize = 25;
            Center(nextText.rectTransform);
            var goal = FightMenuUi.Rect("PracticeObjective", root, objectivePosition, new Vector2(1060, 140));
            Center(goal); objective = goal.gameObject;
            counter = Label(goal, font, Vector2.zero, new Vector2(1040, 140), bodyFontSize);
            var outline = counter.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .9f); outline.effectDistance = new Vector2(2, -2);
            modal.SetActive(false); objective.SetActive(false);
        }
        private static Text Label(Transform parent, Font font, Vector2 position, Vector2 size, int fontSize)
        {
            var label = FightMenuUi.Label(parent, "", position, size, font, fontSize);
            Center(label.rectTransform); label.supportRichText = true; return label;
        }
        private static void Center(RectTransform r) => r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        private static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private void Update()
        {
            if (Completed && isActiveAndEnabled && fight != null && !fight.BattleEnded && !fight.IsPaused)
            {
                if (Gamepad.current?.rightShoulder.wasPressedThisFrame == true) TryEnterNextTutorial();
                return;
            }
            if (!Active || fight == null || ui == null) return;
            if (!started)
            {
                if (fight.AwaitingBattleStart || fight.BattleEntranceRunning || fight.WaveTransitionActive || fight.IsPaused) return;
                started = true; EnterNext();
            }
            if (fight.BattleEnded) { Hide(); return; }
            if (advancePending && !fight.IsPaused) { advancePending = false; EnterNext(); }
        }
        // Called before combat routes A to Fever. Reuses the configured input asset, no extra key binding.
        public bool ConsumeCommand(FightInputRouter.HeroCommand command)
        {
            if (!Active) return false;
            if (!started || pause != null && pause.IsPaused) return true;
            if (Reading)
            {
                if (command == FightInputRouter.HeroCommand.TeamSkill) AdvanceDialogue();
                return true;
            }
            if (command == FightInputRouter.HeroCommand.TeamSkill) return false;
            bool heroCommand = command >= FightInputRouter.HeroCommand.Front && command <= FightInputRouter.HeroCommand.Back;
            return advancePending || (lesson.allowAllHeroCommands ? !heroCommand : command != FightInputRouter.HeroCommand.Front);
        }
        public void AdvanceDialogue()
        {
            if (!Reading || Time.frameCount == enteredFrame || pause != null && pause.IsPaused) return;
            EnterNext();
        }
        private void EnterNext()
        {
            index++; progress = 0; enteredFrame = Time.frameCount;
            if (index >= lesson.steps.Count)
            {
                BeginFreePractice("訓練完成！", "自由練習，或按 <b><color=#FFD879>RB</color></b> 進入下一關教學。"); return;
            }
            fight.SetTutorialChargeSuppressed(!Step.allowSwordCharge);
            fight.ConfigureTutorialEnemies(lesson.controlEnemyAttacks, Step.allowEnemyAttacks, lesson.enemyAttackIntervalBeats, lesson.preventPlayerDeath);
            pause = GetComponent<FightPauseController>();
            if (Step.goal == FightTutorialLesson.Goal.FreePractice)
            { BeginFreePractice(Step.title, Step.text); return; }
            modal.SetActive(Reading); objective.SetActive(!Reading);
            title.text = Step.title; body.text = Step.text; RefreshObjective();
        }
        private void BeginFreePractice(string heading, string description)
        {
            Completed = true;
            fight.ConfigureTutorialEnemies(lesson.controlEnemyAttacks, lesson.attackInFreePractice, lesson.enemyAttackIntervalBeats, lesson.preventPlayerDeath);
            fight.SetTutorialChargeSuppressed(false);
            modal.SetActive(false); objective.SetActive(true);
            SetObjectiveText($"<b><color=#FFD879>{heading}</color></b>\n{description}");
        }
        public bool TryEnterNextTutorial()
        {
            if (!Completed || !isActiveAndEnabled || fight == null || fight.BattleEnded || fight.IsPaused) return false;
            var planner = GetComponent<FightLevelPlanner>();
            if (planner == null || planner.Chapter == null) return false;
            for (int next = planner.StageIndex + 1; next < planner.Chapter.Stages.Count; next++)
            {
                var stage = planner.Chapter.Stages[next];
                if (stage == null || !stage.Tutorial) continue;
                pause = GetComponent<FightPauseController>();
                if (pause != null && pause.TryLoadStage(planner.Chapter, next, out string error)) return true;
                SetObjectiveText("下一關暫時無法載入，請檢查關卡設定。"); return false;
            }
            SetObjectiveText("<b><color=#FFD879>訓練完成！</color></b>\n目前沒有下一個教學關卡，可以繼續自由練習。");
            return false;
        }
        private void RefreshObjective()
        {
            SetObjectiveText($"<b><color=#FFD879>{Step.title}</color></b>\n{Step.text}\n<b><color=#7FE5FF>{progress} / {Mathf.Max(1, Step.required)}</color></b>");
        }
        private void SetObjectiveText(string text)
        {
            counter.text = text;
            // Multi-line instructions must leave room for the final progress line.
            float height = Mathf.Max(140f, counter.preferredHeight + 12f);
            counter.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            ((RectTransform)objective.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
        private void OnHeroCalled(FightCombatController.HeroCallResult call)
        {
            if (!Active || !started || Reading || advancePending || Step.goal != FightTutorialLesson.Goal.OnBeatCalls) return;
            if (call.Command == FightInputRouter.HeroCommand.Front && !call.RhythmResult.DuplicateBeat &&
                call.RhythmResult.Judgement == FmodRhythmJudge.Grade.Perfect) Count();
        }
        private void OnSwordHit(FightUnitSlot actor, bool charged, int issuedFrame)
        {
            // An attack queued before a reading pause must not complete the next objective.
            if (!Active || !started || Reading || advancePending || issuedFrame < enteredFrame || fight.GetHeroForCommand(FightInputRouter.HeroCommand.Front)?.UnitSlot != actor) return;
            if (Step.goal == FightTutorialLesson.Goal.NormalHits && !charged || Step.goal == FightTutorialLesson.Goal.ChargedHits && charged) Count();
        }
        private void OnEnemyHit(FightCombatController.EnemyAttackResult hit)
        { if (Active && started && !Reading && !advancePending && Step.goal == FightTutorialLesson.Goal.SuccessfulBlocks && hit.Blocked) Count(); }
        private void OnSlimeHit(FightUnitSlot target)
        { if (Active && started && !Reading && !advancePending && Step.goal == FightTutorialLesson.Goal.SlimeHits) Count(); }
        private void OnFollowUpHit(FightUnitSlot actor, FightUnitSlot target, int issuedFrame)
        { if (Active && started && !Reading && !advancePending && issuedFrame >= enteredFrame && Step.goal == FightTutorialLesson.Goal.FollowUpHits) Count(); }
        private void OnAimedShotHit(FightUnitSlot actor, bool refreshed, int issuedFrame)
        {
            if (!Active || !started || Reading || advancePending || issuedFrame < enteredFrame ||
                fight.GetHeroForCommand(FightInputRouter.HeroCommand.Front)?.UnitSlot != actor) return;
            if (Step.goal == FightTutorialLesson.Goal.AimedShotHits ||
                Step.goal == FightTutorialLesson.Goal.AimedRefreshHits && refreshed) Count();
        }
        private void Count()
        {
            progress++; RefreshObjective();
            if (progress >= Mathf.Max(1, Step.required)) advancePending = true;
        }
        private void Hide()
        {
            if (ui != null) ui.SetActive(false);
            pause?.SetTutorialReading(false);
            fight?.SetTutorialChargeSuppressed(false);
        }
        private void OnDisable()
        {
            if (fight != null) fight.AimedShotHit -= OnAimedShotHit;
            if (fight != null) { fight.ConfigureTutorialEnemies(false, false, 4, false); fight.AimedFollowUpHit -= OnFollowUpHit; fight.EnemyAttackResolved -= OnEnemyHit; fight.MageSlimeAttached -= OnSlimeHit; fight.HeroCalled -= OnHeroCalled; fight.SwordPrimaryHit -= OnSwordHit; fight.BattleLost -= Hide; fight.BattleVictory -= Hide; }
            Hide();
        }
        private void OnDestroy()
        { if (ui != null) Destroy(ui); if (runtimeFont != null) Destroy(runtimeFont); }
    }
}
