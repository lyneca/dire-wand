using System.Collections;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public enum SpellType {
    Button,
    Trigger
}

public class WandSkill : SkillData {
    protected WandBehaviour wand;
    protected Item wandItem;
    public int order = 0;

    public override void OnCatalogRefresh()
    {
        base.OnCatalogRefresh();
        while (primarySkillTree.tierBlockers.Count < tier + 1)
        {
            primarySkillTree.tierBlockers.Add(null);
        }
    }

    public override IEnumerator OnCatalogRefreshCoroutine()
    {
        yield break;
    }

    public override void OnSkillLoaded(SkillData skillData, Creature creature)
    {
        base.OnSkillLoaded(skillData, creature);
        WandBehaviour.InvokeWandSkillReload();
    }

    public override void OnSkillUnloaded(SkillData skillData, Creature creature)
    {
        base.OnSkillUnloaded(skillData, creature);
        WandBehaviour.InvokeWandSkillReload();
    }

    // public string[] gesture;
    public SpellType type;
    public Color color;
    // public bool showInTutorial = true;

    public override CatalogData Clone() {
        return MemberwiseClone() as WandSkill;
    }

    public void MarkCasted() {
        // TutorialSave.Cast(tier, id);
    }

    public void Begin(WandBehaviour wand) {
        this.wand = wand;
        wandItem = wand.item;
        if (color == default) {
            switch (type) {
                case SpellType.Button:
                    color = Utils.HexColor(191, 119, 30, 3);
                    break;
                case SpellType.Trigger:
                    color = Utils.HexColor(40, 30, 191, 3);
                    break;
            }
        }
    }

    public virtual void OnInit() {}
    public virtual void OnUpdate() {}
    public virtual void OnReset() {}
}