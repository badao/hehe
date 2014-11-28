using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace Master
{
    class XinZhao : Program
    {
        public XinZhao()
        {
            SkillQ = new Spell(SpellSlot.Q, 375);
            SkillW = new Spell(SpellSlot.W, 20);
            SkillE = new Spell(SpellSlot.E, 650);
            SkillR = new Spell(SpellSlot.R, 500);
            SkillE.SetTargetted(SkillE.Instance.SData.SpellCastTime, SkillE.Instance.SData.MissileSpeed);
            SkillR.SetSkillshot(SkillR.Instance.SData.SpellCastTime, SkillR.Instance.SData.LineWidth, SkillR.Instance.SData.MissileSpeed, false, SkillshotType.SkillshotCircle);

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem("qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("rusage", "Use R To Finish").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearW", "Use W").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearE", "Use E").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearI", "Use Tiamat/Hydra Item").SetValue(true));

            Config.AddSubMenu(new Menu("Ultimate", "useUlt"));
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
            {
                Config.SubMenu("useUlt").AddItem(new MenuItem("ult" + enemy.ChampionName, "Use Ultimate On " + enemy.ChampionName).SetValue(true));
            }

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem("useInterR", "Use R To Interrupt").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("killstealE", "Auto E To Kill Steal").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem("CustomSkin", "Skin Changer").SetValue(new Slider(5, 0, 5))).ValueChanged += SkinChanger;

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawE", "E Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawR", "R Range").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze) LaneJungClear();
            if (Config.Item("killstealE").GetValue<bool>()) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item("DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (Config.Item("DrawR").GetValue<bool>() && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!Config.Item("useInterR").GetValue<bool>()) return;
            if (unit.IsValidTarget(SkillR.Range) && SkillR.IsReady() && !unit.HasBuff("xenzhaointimidate")) SkillR.Cast(PacketCast());
        }

        private void AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (!unit.IsMe) return;
            if (Config.Item("qusage").GetValue<bool>() && target.IsValidTarget(SkillQ.Range) && SkillQ.IsReady() && (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)) SkillQ.Cast(PacketCast());
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (Config.Item("rusage").GetValue<bool>() && Config.Item("ult" + targetObj.ChampionName).GetValue<bool>() && SkillR.IsReady() && SkillR.InRange(targetObj.Position))
            {
                if (CanKill(targetObj, SkillR))
                {
                    SkillR.Cast(PacketCast());
                }
                else if (targetObj.Health - SkillR.GetDamage(targetObj) <= SkillE.GetDamage(targetObj) + Player.GetAutoAttackDamage(targetObj) + SkillQ.GetDamage(targetObj) * 3 && Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && Config.Item("qusage").GetValue<bool>() && SkillQ.IsReady()) SkillR.Cast(PacketCast());
            }
            if (Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && (Player.Distance(targetObj) > 450 || CanKill(targetObj, SkillE))) SkillE.CastOnUnit(targetObj, PacketCast());
            if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady() && Orbwalk.InAutoAttackRange(targetObj)) SkillW.Cast(PacketCast());
            if (Config.Item("iusage").GetValue<bool>()) UseItem(targetObj);
            if (Config.Item("ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, SkillE.Range, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault();
            if (minionObj == null) return;
            if (Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady() && (Player.Distance(minionObj) > 450 || CanKill(minionObj, SkillE))) SkillE.CastOnUnit(minionObj, PacketCast());
            if (Config.Item("useClearW").GetValue<bool>() && SkillW.IsReady() && Orbwalk.InAutoAttackRange(minionObj)) SkillW.Cast(PacketCast());
            if (Config.Item("useClearQ").GetValue<bool>() && SkillQ.IsReady() && SkillQ.InRange(minionObj.Position)) SkillQ.Cast(PacketCast());
            if (Config.Item("useClearI").GetValue<bool>() && Player.Distance(minionObj) <= 350)
            {
                if (Items.CanUseItem(Tiamat)) Items.UseItem(Tiamat);
                if (Items.CanUseItem(Hydra)) Items.UseItem(Hydra);
            }
        }

        private void KillSteal()
        {
            var target = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(i => i.IsValidTarget(SkillE.Range) && CanKill(i, SkillE) && i != targetObj);
            if (target != null && SkillE.IsReady()) SkillE.CastOnUnit(target, PacketCast());
        }

        private void UseItem(Obj_AI_Hero target)
        {
            if (Items.CanUseItem(Bilge) && Player.Distance(target) <= 450) Items.UseItem(Bilge, target);
            if (Items.CanUseItem(Blade) && Player.Distance(target) <= 450) Items.UseItem(Blade, target);
            if (Items.CanUseItem(Tiamat) && Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(target) < target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (Items.CanUseItem(Youmuu) && Player.CountEnemysInRange(350) >= 1) Items.UseItem(Youmuu);
        }
    }
}