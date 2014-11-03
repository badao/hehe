using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using LX_Orbwalker;

namespace Master
{
    class Rammus : Program
    {
        private const String Version = "1.0.0";

        public Rammus()
        {
            SkillQ = new Spell(SpellSlot.Q, 1100);
            SkillW = new Spell(SpellSlot.W, 325);
            SkillE = new Spell(SpellSlot.E, 300);
            SkillR = new Spell(SpellSlot.R, 300);
            SkillE.SetTargetted(SkillE.Instance.SData.SpellCastTime, SkillE.Instance.SData.MissileSpeed);
            SkillR.SetSkillshot(SkillR.Instance.SData.SpellCastTime, SkillR.Instance.SData.LineWidth, SkillR.Instance.SData.MissileSpeed, false, SkillshotType.SkillshotCircle);

            Config.AddSubMenu(new Menu("Combo/Harass", "csettings"));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "qusage", "Use Q").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "wusage", "Use W").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "eusage", "Use E").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "euseMode", "E Mode").SetValue(new StringList(new[] { "Always", "W Ready" })));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "autoeusage", "Use E If Hp Above").SetValue(new Slider(20, 1)));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "rusage", "Use R").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "ruseMode", "R Mode").SetValue(new StringList(new[] { "Always", "# Enemy" })));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "rmulti", "Use R If Enemy Above").SetValue(new Slider(2, 1, 4)));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "ignite", "Auto Ignite If Killable").SetValue(true));
            Config.SubMenu("csettings").AddItem(new MenuItem(Name + "iusage", "Use Item").SetValue(true));

            Config.AddSubMenu(new Menu("Lane/Jungle Clear", "LaneJungClear"));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearQ", "Use Q").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearW", "Use W").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearE", "Use E").SetValue(true));
            Config.SubMenu("LaneJungClear").AddItem(new MenuItem(Name + "useClearEMode", "E Mode").SetValue(new StringList(new[] { "Always", "W Ready" })));

            Config.AddSubMenu(new Menu("Misc", "miscs"));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "useAntiQ", "Use Q To Anti Gap Closer").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "useInterE", "Use E To Interrupt").SetValue(true));
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "SkinID", "Skin Changer").SetValue(new Slider(6, 0, 6))).ValueChanged += SkinChanger;
            Config.SubMenu("miscs").AddItem(new MenuItem(Name + "packetCast", "Use Packet To Cast").SetValue(true));

            Config.AddSubMenu(new Menu("Draw", "DrawSettings"));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawE", "E Range").SetValue(true));
            Config.SubMenu("DrawSettings").AddItem(new MenuItem(Name + "DrawR", "R Range").SetValue(true));

            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
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
            else if (LXOrbwalker.CurrentMode == LXOrbwalker.Mode.Flee && SkillQ.IsReady() && !Player.HasBuff("PowerBall", true)) SkillQ.Cast();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (Config.Item(Name + "DrawE").GetValue<bool>() && SkillE.Level > 0) Utility.DrawCircle(Player.Position, SkillE.Range, SkillE.IsReady() ? Color.Green : Color.Red);
            if (Config.Item(Name + "DrawR").GetValue<bool>() && SkillR.Level > 0) Utility.DrawCircle(Player.Position, SkillR.Range, SkillR.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!Config.Item(Name + "useAntiQ").GetValue<bool>()) return;
            if (gapcloser.Sender.IsValidTarget(SkillE.Range) && SkillQ.IsReady() && !Player.HasBuff("PowerBall", true)) SkillQ.Cast(PacketCast);
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!Config.Item(Name + "useInterE").GetValue<bool>()) return;
            if (unit.IsValidTarget(SkillE.Range) && SkillE.IsReady()) SkillE.CastOnUnit(unit, PacketCast);
        }

        private void NormalCombo()
        {
            if (targetObj == null) return;
            if (Config.Item(Name + "qusage").GetValue<bool>() && SkillQ.IsReady() && targetObj.IsValidTarget(1000) && !Player.HasBuff("PowerBall", true))
            {
                if (!SkillE.InRange(targetObj.Position))
                {
                    SkillQ.Cast(PacketCast);
                }
                else if (!Player.HasBuff("DefensiveBallCurl", true)) SkillQ.Cast(PacketCast);
            }
            if (Config.Item(Name + "eusage").GetValue<bool>() && SkillE.IsReady() && SkillE.InRange(targetObj.Position) && Player.Health * 100 / Player.MaxHealth >= Config.Item(Name + "autoeusage").GetValue<Slider>().Value)
            {
                switch (Config.Item(Name + "euseMode").GetValue<StringList>().SelectedIndex)
                {
                    case 0:
                        SkillE.CastOnUnit(targetObj, PacketCast);
                        break;
                    case 1:
                        if (Player.HasBuff("DefensiveBallCurl", true)) SkillE.CastOnUnit(targetObj, PacketCast);
                        break;
                }
            }
            if (Config.Item(Name + "wusage").GetValue<bool>() && SkillW.IsReady() && SkillE.InRange(targetObj.Position) && !Player.HasBuff("PowerBall", true)) SkillW.Cast();
            if (Config.Item(Name + "rusage").GetValue<bool>() && SkillR.IsReady())
            {
                switch (Config.Item(Name + "ruseMode").GetValue<StringList>().SelectedIndex)
                {
                    case 0:
                        if (SkillR.InRange(targetObj.Position)) SkillR.Cast(PacketCast);
                        break;
                    case 1:
                        if (Player.CountEnemysInRange((int)SkillR.Range) >= Config.Item(Name + "rmulti").GetValue<Slider>().Value) SkillR.Cast(PacketCast);
                        break;
                }
            }
            if (Config.Item(Name + "iusage").GetValue<bool>() && Items.CanUseItem(Rand) && Player.CountEnemysInRange(450) >= 1) Items.UseItem(Rand);
            if (Config.Item(Name + "ignite").GetValue<bool>()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Player.Position, 1000, MinionTypes.All, MinionTeam.NotAlly).FirstOrDefault();
            if (minionObj == null) return;
            if (Config.Item(Name + "useClearQ").GetValue<bool>() && SkillQ.IsReady() && !Player.HasBuff("PowerBall", true))
            {
                if (!SkillE.InRange(minionObj.Position))
                {
                    SkillQ.Cast(PacketCast);
                }
                else if (!Player.HasBuff("DefensiveBallCurl", true)) SkillQ.Cast(PacketCast);
            }
            if (Config.Item(Name + "useClearE").GetValue<bool>() && SkillE.IsReady() && SkillE.InRange(minionObj.Position))
            {
                switch (Config.Item(Name + "useClearEMode").GetValue<StringList>().SelectedIndex)
                {
                    case 0:
                        SkillE.CastOnUnit(minionObj, PacketCast);
                        break;
                    case 1:
                        if (Player.HasBuff("DefensiveBallCurl", true)) SkillE.CastOnUnit(minionObj, PacketCast);
                        break;
                }
            }
            if (Config.Item(Name + "useClearW").GetValue<bool>() && SkillW.IsReady() && SkillE.InRange(minionObj.Position) && !Player.HasBuff("PowerBall", true)) SkillW.Cast(PacketCast);
        }
    }
}