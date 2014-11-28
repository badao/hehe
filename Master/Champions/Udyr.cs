using System;
using System.Linq;

using LeagueSharp;
using LeagueSharp.Common;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace Master
{
    class Udyr : Program
    {
        private enum Stance
        {
            Tiger,
            Turtle,
            Bear,
            Phoenix
        }
        private Stance CurStance;
        private Int32 AACount = 0;
        private bool TigerActive = false, PhoenixActive = false;
        private Obj_AI_Base minionObj;

        public Udyr()
        {
            SkillQ = new Spell(SpellSlot.Q, 600);
            SkillW = new Spell(SpellSlot.W, 600);
            SkillE = new Spell(SpellSlot.E, 600);
            SkillR = new Spell(SpellSlot.R, 600);

            Config.SubMenu("Orbwalker").SubMenu("xSLxOrbwalker_Modes").AddItem(new MenuItem("stunActive", "Stun Cycle").SetValue(new KeyBind("Z".ToCharArray()[0], KeyBindType.Press)));

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem("qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("rusage", "Use R").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearW", "Use W").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearE", "Use E").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearR", "Use R").SetValue(true));

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem("useAntiE", "Use E To Anti Gap Closer").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("useInterE", "Use E To Interrupt").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("surviveW", "Try Use W To Survive").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Obj_AI_Base.OnCreate += OnCreate;
            Obj_AI_Base.OnDelete += OnDelete;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo)
            {
                NormalCombo();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(true);
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) Flee();
            if (Config.Item("stunActive").GetValue<KeyBind>().Active)
            {
                StunCycle();
            }
            else Orbwalk.CustomMode = false;
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!Config.Item("useAntiE").GetValue<bool>()) return;
            if (gapcloser.Sender.IsValidTarget(400) && !gapcloser.Sender.HasBuff("udyrbearstuncheck", true) && (SkillE.IsReady() || CurStance == Stance.Bear))
            {
                if (CurStance != Stance.Bear) SkillE.Cast(PacketCast());
                if (CurStance == Stance.Bear) Player.IssueOrder(GameObjectOrder.AttackUnit, gapcloser.Sender);
            }
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!Config.Item("useInterE").GetValue<bool>()) return;
            if (unit.IsValidTarget(SkillE.Range) && !unit.HasBuff("udyrbearstuncheck", true) && (SkillE.IsReady() || CurStance == Stance.Bear))
            {
                if (CurStance != Stance.Bear) SkillE.Cast(PacketCast());
                if (CurStance == Stance.Bear) Player.IssueOrder(GameObjectOrder.AttackUnit, unit);
            }
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.Name == SkillQ.Instance.Name)
            {
                CurStance = Stance.Tiger;
                AACount = 0;
            }
            else if (args.SData.Name == SkillW.Instance.Name)
            {
                CurStance = Stance.Turtle;
                AACount = 0;
            }
            else if (args.SData.Name == SkillE.Instance.Name)
            {
                CurStance = Stance.Bear;
                AACount = 0;
            }
            else if (args.SData.Name == SkillR.Instance.Name)
            {
                CurStance = Stance.Phoenix;
                AACount = 0;
            }
        }

        private void OnCreate(GameObject sender, EventArgs args)
        {
            if (Player.Distance(sender.Position) <= 70 && (sender.Name == "Udyr_PhoenixBreath_cas.troy" || sender.Name == "Udyr_Spirit_Phoenix_Breath_cas.troy")) PhoenixActive = true;
            if (Player.Distance(sender.Position) <= 450 && (sender.Name == "udyr_tiger_claw_tar.troy" || sender.Name == "Udyr_Spirit_Tiger_Claw_tar.troy")) TigerActive = true;
            if (sender is Obj_SpellMissile && sender.IsValid && Config.Item("surviveW").GetValue<bool>() && SkillW.IsReady())
            {
                var missle = (Obj_SpellMissile)sender;
                var caster = missle.SpellCaster;
                if (caster.IsEnemy)
                {
                    var ShieldBuff = new Int32[] { 60, 100, 140, 180, 220 }[SkillW.Level - 1] + 0.5 * Player.FlatMagicDamageMod;
                    if (missle.SData.Name.Contains("BasicAttack"))
                    {
                        if (missle.Target.IsMe && Player.Health <= caster.GetAutoAttackDamage(Player, true) && Player.Health + ShieldBuff > caster.GetAutoAttackDamage(Player, true)) SkillW.Cast();
                    }
                    else if (missle.Target.IsMe || missle.EndPosition.Distance(Player.Position) <= 130)
                    {
                        if (missle.SData.Name == "summonerdot")
                        {
                            if (Player.Health <= (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite) && Player.Health + ShieldBuff > (caster as Obj_AI_Hero).GetSummonerSpellDamage(Player, Damage.SummonerSpell.Ignite)) SkillW.Cast();
                        }
                        else if (Player.Health <= (caster as Obj_AI_Hero).GetSpellDamage(Player, (caster as Obj_AI_Hero).GetSpellSlot(missle.SData.Name, false), 1) && Player.Health + ShieldBuff > (caster as Obj_AI_Hero).GetSpellDamage(Player, (caster as Obj_AI_Hero).GetSpellSlot(missle.SData.Name, false), 1)) SkillW.Cast();
                    }
                }
            }
        }

        private void OnDelete(GameObject sender, EventArgs args)
        {
            if (Player.Distance(sender.Position) <= 70 && (sender.Name == "Udyr_PhoenixBreath_cas.troy" || sender.Name == "Udyr_Spirit_Phoenix_Breath_cas.troy")) PhoenixActive = false;
            if (Player.Distance(sender.Position) <= 450 && (sender.Name == "udyr_tiger_claw_tar.troy" || sender.Name == "Udyr_Spirit_Tiger_Claw_tar.troy")) TigerActive = false;
        }


        private void AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (!unit.IsMe) return;
            if (Orbwalk.CurrentMode != Orbwalk.Mode.Flee && Orbwalk.CurrentMode != Orbwalk.Mode.LastHit && Orbwalk.CurrentMode != Orbwalk.Mode.None)
            {
                if ((target == targetObj || target == minionObj) && (CurStance == Stance.Tiger || CurStance == Stance.Phoenix)) AACount += 1;
            }
        }

        private void NormalCombo(bool IsHarass = false)
        {
            if (targetObj == null) return;
            if (Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && !targetObj.HasBuff("udyrbearstuncheck", true) && targetObj.IsValidTarget(IsHarass ? 450 : 800)) SkillE.Cast(PacketCast());
            if (targetObj.IsValidTarget(400) && (!Config.Item("eusage").GetValue<bool>() || (Config.Item("eusage").GetValue<bool>() && (SkillE.Level == 0 || (SkillE.Level >= 1 && targetObj.HasBuff("udyrbearstuncheck", true))))))
            {
                if (Config.Item("qusage").GetValue<bool>() && SkillQ.IsReady()) SkillQ.Cast(PacketCast());
                if (Config.Item("rusage").GetValue<bool>() && SkillR.IsReady() && SkillQ.Level >= 1 && CurStance == Stance.Tiger && (AACount >= 2 || TigerActive))
                {
                    SkillR.Cast(PacketCast());
                }
                else if (SkillQ.Level == 0 && SkillR.IsReady()) SkillR.Cast(PacketCast());
                if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady())
                {
                    if (CurStance == Stance.Phoenix && (AACount >= 3 || PhoenixActive))
                    {
                        SkillW.Cast(PacketCast());
                    }
                    else if (CurStance == Stance.Tiger && (AACount >= 2 || TigerActive))
                    {
                        SkillW.Cast(PacketCast());
                    }
                    else if (SkillQ.Level == 0 && SkillR.Level == 0) SkillW.Cast(PacketCast());
                }
            }
            if (Config.Item("iusage").GetValue<bool>()) UseItem(targetObj);
            if (Config.Item("ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            minionObj = MinionManager.GetMinions(Player.Position, 800, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault();
            if (minionObj == null) return;
            Orbwalk.ForcedTarget = minionObj;
            if (Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady() && !minionObj.HasBuff("udyrbearstuncheck", true)) SkillE.Cast(PacketCast());
            if (minionObj.IsValidTarget(400) && (!Config.Item("useClearE").GetValue<bool>() || (Config.Item("useClearE").GetValue<bool>() && (SkillE.Level == 0 || (SkillE.Level >= 1 && minionObj.HasBuff("udyrbearstuncheck", true))))))
            {
                if (Config.Item("useClearQ").GetValue<bool>() && SkillQ.IsReady()) SkillQ.Cast(PacketCast());
                if (Config.Item("useClearR").GetValue<bool>() && SkillR.IsReady() && SkillQ.Level >= 1 && CurStance == Stance.Tiger && (AACount >= 2 || TigerActive))
                {
                    SkillR.Cast(PacketCast());
                }
                else if (SkillQ.Level == 0 && SkillR.IsReady()) SkillR.Cast(PacketCast());
                if (Config.Item("useClearW").GetValue<bool>() && SkillW.IsReady())
                {
                    if (CurStance == Stance.Phoenix && (AACount >= 3 || PhoenixActive))
                    {
                        SkillW.Cast(PacketCast());
                    }
                    else if (CurStance == Stance.Tiger && (AACount >= 2 || TigerActive))
                    {
                        SkillW.Cast(PacketCast());
                    }
                    else if (SkillQ.Level == 0 && SkillR.Level == 0) SkillW.Cast(PacketCast());
                }
            }
        }

        private void Flee()
        {
            var manaQ = SkillQ.Instance.ManaCost;
            var manaW = SkillW.Instance.ManaCost;
            var manaR = SkillR.Instance.ManaCost;
            var PData = Player.Buffs.FirstOrDefault(i => i.Name == "udyrmonkeyagilitybuff");
            if (SkillE.IsReady()) SkillE.Cast(PacketCast());
            if (PData != null && PData.Count < 3)
            {
                if ((manaQ < manaW || manaQ < manaR || (manaQ == manaW && manaQ < manaR) || (manaQ == manaR && manaQ < manaW)) && SkillQ.IsReady())
                {
                    SkillQ.Cast(PacketCast());
                }
                else if ((manaW < manaQ || manaW < manaR || (manaW == manaQ && manaW < manaR) || (manaW == manaR && manaW < manaQ)) && SkillW.IsReady())
                {
                    SkillW.Cast(PacketCast());
                }
                else if ((manaR < manaQ || manaR < manaW || (manaR == manaQ && manaR < manaW) || (manaR == manaW && manaR < manaQ)) && SkillR.IsReady()) SkillR.Cast(PacketCast());
            }
        }

        private void StunCycle()
        {
            var targetClosest = ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(800) && !i.HasBuff("udyrbearstuncheck", true)).OrderBy(i => i.Distance(Player)).FirstOrDefault();
            CustomOrbwalk(targetClosest);
            if (targetClosest == null) return;
            if (SkillE.IsReady()) SkillE.Cast(PacketCast());
        }

        private void UseItem(Obj_AI_Hero target)
        {
            if (Items.CanUseItem(Bilge) && Player.Distance(target) <= 450) Items.UseItem(Bilge, target);
            if (Items.CanUseItem(Blade) && Player.Distance(target) <= 450) Items.UseItem(Blade, target);
            if (Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
        }
    }
}