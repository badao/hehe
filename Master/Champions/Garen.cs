using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterCommon.M_Orbwalker;

namespace Master
{
    class Garen : Program
    {
        public Garen()
        {
            SkillQ = new Spell(SpellSlot.Q, 20);
            SkillW = new Spell(SpellSlot.W, 20);
            SkillE = new Spell(SpellSlot.E, 300);
            SkillR = new Spell(SpellSlot.R, 400);
            SkillR.SetTargetted(SkillR.Instance.SData.SpellCastTime, SkillR.Instance.SData.MissileSpeed);

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem("qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("autowusage", "Use W If Hp Under").SetValue(new Slider(60, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem("eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("rusage", "Use R To Finish").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem("iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem("useClearE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Ultimate", "useUlt"));
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
            {
                Config.SubMenu("useUlt").AddItem(new MenuItem("ult" + enemy.ChampionName, "Use Ultimate On " + enemy.ChampionName).SetValue(true));
            }

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem("CustomSkin", "Skin Changer").SetValue(new Slider(6, 0, 6))).ValueChanged += SkinChanger;

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawE", "E Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem("DrawR", "R Range").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen) return;
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear || Orbwalk.CurrentMode == Orbwalk.Mode.LaneFreeze)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee && SkillQ.IsReady()) SkillQ.Cast();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item("DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (Config.Item("DrawR").GetValue<bool>() && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (!unit.IsMe) return;
            if (Config.Item("qusage").GetValue<bool>() && SkillQ.IsReady() && target.IsValidTarget(Orbwalk.GetAutoAttackRange(Player, target)) && (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)) SkillQ.Cast(PacketCast());
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (Config.Item("qusage").GetValue<bool>() && SkillQ.IsReady() && targetObj.IsValidTarget(1000) && !Orbwalk.InAutoAttackRange(targetObj)) SkillQ.Cast(PacketCast());
            if (Config.Item("eusage").GetValue<bool>() && SkillE.IsReady() && !Player.HasBuff("GarenE") && !Player.HasBuff("GarenQ", true) && SkillE.InRange(targetObj.Position)) SkillE.Cast(PacketCast());
            if (Config.Item("wusage").GetValue<bool>() && SkillW.IsReady() && Orbwalk.InAutoAttackRange(targetObj) && Player.Health * 100 / Player.MaxHealth <= Config.Item("autowusage").GetValue<Slider>().Value) SkillW.Cast(PacketCast());
            if (Config.Item("rusage").GetValue<bool>() && Config.Item("ult" + targetObj.ChampionName).GetValue<bool>() && SkillR.IsReady() && SkillR.InRange(targetObj.Position) && CanKill(targetObj, SkillR)) SkillR.CastOnUnit(targetObj, PacketCast());
            if (Config.Item("iusage").GetValue<bool>() && Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (Config.Item("ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            foreach (var minionObj in MinionManager.GetMinions(Player.Position, 800, MinionTypes.All, MinionTeam.NotAlly))
            {
                if (Config.Item("useClearQ").GetValue<bool>())
                {
                    if (CanKill(minionObj, SkillQ) && Orbwalk.InAutoAttackRange(minionObj))
                    {
                        if (SkillQ.IsReady() || Player.HasBuff("GarenQ", true))
                        {
                            Orbwalk.SetAttack(false);
                            if (!Player.HasBuff("GarenQ", true)) SkillQ.Cast(PacketCast());
                            if (Player.HasBuff("GarenQ", true)) Player.IssueOrder(GameObjectOrder.AttackUnit, minionObj);
                            Orbwalk.SetAttack(true);
                        }
                    }
                    else if (Player.Distance(minionObj) > 450 && SkillQ.IsReady()) SkillQ.Cast();
                }
                if (Config.Item("useClearE").GetValue<bool>() && SkillE.IsReady() && !Player.HasBuff("GarenE") && !Player.HasBuff("GarenQ", true) && SkillE.InRange(minionObj.Position)) SkillE.Cast(PacketCast());
            }
        }
    }
}