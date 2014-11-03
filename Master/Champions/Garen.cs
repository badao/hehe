using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using LX_Orbwalker;

namespace Master
{
    class Garen : Program
    {
        private const String Version = "1.0.0";

        public Garen()
        {
            SkillQ = new Spell(SpellSlot.Q, 20);
            SkillW = new Spell(SpellSlot.W, 20);
            SkillE = new Spell(SpellSlot.E, 300);
            SkillR = new Spell(SpellSlot.R, 400);
            SkillR.SetTargetted(SkillR.Instance.SData.SpellCastTime, SkillR.Instance.SData.MissileSpeed);

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "autowusage", "Use W If Hp Under").SetValue(new Slider(60, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "rusage", "Use R To Finish").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearE", "Use E").SetValue(true));

            Config.AddSubMenu(new Menu("Ultimate", "useUlt"));
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsEnemy))
            {
                Config.SubMenu("useUlt").AddItem(new MenuItem(Name + "ult" + enemy.ChampionName, "Use Ultimate On " + enemy.ChampionName).SetValue(true));
            }

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "SkinID", "Skin Changer").SetValue(new Slider(6, 0, 6))).ValueChanged += SkinChanger;
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "packetCast", "Use Packet To Cast").SetValue(true));

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawE", "E Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawR", "R Range").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            LXOrbwalker.AfterAttack += AfterAttack;
            Game.PrintChat("<font color = \"#33CCCC\">Master of {0}</font> <font color = \"#00ff00\">v{1}</font>", Name, Version);
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead) return;
            PacketCast = Config.Item(Name + "packetCast").GetValue<bool>();
            if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Combo || LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Harass)
            {
                NormalCombo();
            }
            else if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.LaneClear || LXOrbwalker.CurrentMode == LXOrbwalker.Mode.LaneFreeze)
            {
                LaneJungClear();
            }
            else if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Flee && SkillQ.IsReady()) SkillQ.Cast();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item(Name + "DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "DrawR").GetValue<bool>() && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void AfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (unit.IsMe && Config.Item(Name + "qusage").GetValue<bool>() && SkillQ.IsReady() && target.IsValidTarget(LXOrbwalker.GetAutoAttackRange(Player, target)) && (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Combo || LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Harass)) SkillQ.Cast(PacketCast);
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (Config.Item(Name + "qusage").GetValue<bool>() && SkillQ.IsReady() && targetObj.IsValidTarget(1000) && !LXOrbwalker.InAutoAttackRange(targetObj)) SkillQ.Cast(PacketCast);
            if (Config.Item(Name + "eusage").GetValue<bool>() && SkillE.IsReady() && !Player.HasBuff("GarenE", true) && !Player.HasBuff("GarenQ", true) && SkillE.InRange(targetObj.Position)) SkillE.Cast(PacketCast);
            if (Config.Item(Name + "wusage").GetValue<bool>() && SkillW.IsReady() && SkillE.InRange(targetObj.Position) && Player.Health * 100 / Player.MaxHealth <= Config.Item(Name + "autowusage").GetValue<Slider>().Value) SkillW.Cast(PacketCast);
            if (Config.Item(Name + "rusage").GetValue<bool>() && Config.Item(Name + "ult" + targetObj.ChampionName).GetValue<bool>() && SkillR.IsReady() && SkillR.InRange(targetObj.Position) && CanKill(targetObj, SkillR)) SkillR.CastOnUnit(targetObj, PacketCast);
            if (Config.Item(Name + "iusage").GetValue<bool>() && Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (Config.Item(Name + "ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, 800, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault();
            if (minionObj == null) return;
            if (Config.Item(Name + "useClearQ").GetValue<bool>() && SkillQ.IsReady()) SkillQ.Cast(PacketCast);
            if (Config.Item(Name + "useClearE").GetValue<bool>() && SkillE.IsReady() && !Player.HasBuff("GarenE", true) && !Player.HasBuff("GarenQ", true) && SkillE.InRange(minionObj.Position)) SkillE.Cast(PacketCast);
        }
    }
}