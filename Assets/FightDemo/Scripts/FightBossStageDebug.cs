using UnityEngine;

namespace RhythmHunter.FightDemo
{
    [DisallowMultipleComponent]
    public sealed class FightBossStageDebug : MonoBehaviour
    {
        [SerializeField] private FightCombatController fight;
        [Tooltip("Testing only: complete phase one once the opening has finished. No reward damage is recorded.")]
        [SerializeField] private bool completePhaseOneOnStart;
        [Tooltip("Testing only: queue a direct phase-two request for its controller. Until phase two is implemented, this only completes phase one.")]
        [SerializeField] private bool skipToPhaseTwoOnStart;
        private bool applied;
        public bool PhaseTwoSkipRequested { get; private set; }
        public bool CanTest => Application.isPlaying && fight && !fight.AwaitingBattleStart &&
            fight.RosterManager && fight.RosterManager.IsBossStage && !fight.IsPaused;
        private void Update()
        {
            if(!CanTest || applied || (!completePhaseOneOnStart && !skipToPhaseTwoOnStart))return;
            applied=CompletePhaseOne();
            if(applied && skipToPhaseTwoOnStart)PhaseTwoSkipRequested=true;
        }
        public bool CompletePhaseOne()
        {
            if(!CanTest)return false;
            bool completed=false;
            foreach(var enemy in fight.RosterManager.ActiveEnemies)
                if(enemy && enemy.BossPhaseOneProtected)completed|=enemy.DebugCompleteBossPhaseOne();
            return completed;
        }
        public void RequestPhaseTwoSkip()
        {
            if(CompletePhaseOne())PhaseTwoSkipRequested=true;
        }
        public bool ConsumePhaseTwoSkipRequest()
        {
            bool requested=PhaseTwoSkipRequested;PhaseTwoSkipRequested=false;return requested;
        }
        private void OnDisable() { applied=false;PhaseTwoSkipRequested=false; }
    }
}
